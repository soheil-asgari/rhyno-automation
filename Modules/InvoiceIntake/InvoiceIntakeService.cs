using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OfficeAutomation.Modules.Ocr;

namespace OfficeAutomation.Modules.InvoiceIntake;

/// <summary>
/// Orchestrates the tax &amp; invoice intake pipeline: file validation, storage,
/// OCR processing, field extraction and schema validation.
/// </summary>
public sealed class InvoiceIntakeService
{
    public const int MaxFilesPerOwner = 50;
    private const long DefaultMaxFileBytes = 20 * 1024 * 1024;

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".pdf"
    };

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/png",
        "image/jpeg",
        "application/pdf",
        "application/octet-stream" // some clients omit image/... mime for rare extensions
    };

    private readonly IWebHostEnvironment _environment;
    private readonly IIntakeSessionStore _store;
    private readonly IOcrEngine _ocrEngine;
    private readonly InvoiceFieldExtractor _fieldExtractor;
    private readonly InvoiceJsonSchemaValidator _schemaValidator;
    private readonly OcrOptions _ocrOptions;
    private readonly ILogger<InvoiceIntakeService> _logger;

    public InvoiceIntakeService(
        IWebHostEnvironment environment,
        IIntakeSessionStore store,
        IOcrEngine ocrEngine,
        IOptions<OcrOptions> ocrOptions,
        ILogger<InvoiceIntakeService> logger)
    {
        _environment = environment;
        _store = store;
        _ocrEngine = ocrEngine;
        _ocrOptions = ocrOptions.Value;
        _fieldExtractor = new InvoiceFieldExtractor();
        _schemaValidator = new InvoiceJsonSchemaValidator();
        _logger = logger;
    }

    public long MaxFileBytes => _ocrOptions.MaxFileBytes > 0 ? _ocrOptions.MaxFileBytes : DefaultMaxFileBytes;

    /// <summary>
    /// Validates and stores an uploaded file. Returns an error message on failure (null on success).
    /// </summary>
    public InvoiceIntakeItem? ValidateAndStore(IFormFile file, string ownerKey, out string? error)
    {
        error = null;

        if (file is null)
        {
            error = "فایلی دریافت نشد.";
            return null;
        }

        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
        {
            error = "پسوند فایل مجاز نیست. فقط PNG، JPG و PDF پشتیبانی میشوند.";
            return null;
        }

        if (!string.IsNullOrWhiteSpace(file.ContentType) && !AllowedContentTypes.Contains(file.ContentType))
        {
            error = "نوع محتوای فایل مجاز نیست.";
            return null;
        }

        if (file.Length <= 0)
        {
            error = "فایل ارسالی خالی است.";
            return null;
        }

        if (file.Length > MaxFileBytes)
        {
            error = $"حجم فایل بیش از حد مجاز ({MaxFileBytes / (1024 * 1024)} مگابایت) است.";
            return null;
        }

        if (_store.Count(ownerKey) >= MaxFilesPerOwner)
        {
            error = $"حداکثر {MaxFilesPerOwner} فایل قابل بارگذاری است.";
            return null;
        }

        var safeOwner = SanitizeOwnerKey(ownerKey);
        var storedFileName = $"{Guid.NewGuid():N}{extension}";
        var relativeDir = Path.Combine("uploads", "intake", safeOwner);
        var dir = Path.Combine(_environment.WebRootPath, relativeDir);
        Directory.CreateDirectory(dir);

        var storedPath = Path.Combine(dir, storedFileName);
        try
        {
            using var stream = File.Create(storedPath);
            file.CopyTo(stream);
        }
        catch (Exception ex)
        {
            error = $"خطا در ذخیرهسازی فایل: {ex.Message}";
            return null;
        }

        var item = new InvoiceIntakeItem
        {
            OriginalFileName = Path.GetFileName(file.FileName),
            StoredFileName = storedFileName,
            StoredRelativePath = Path.Combine(relativeDir, storedFileName),
            ContentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
            Extension = extension,
            FileSize = file.Length,
            Status = IntakeFileStatus.Pending
        };

        if (!_store.Add(ownerKey, item, MaxFilesPerOwner))
        {
            error = $"حداکثر {MaxFilesPerOwner} فایل قابل بارگذاری است.";
            try { File.Delete(storedPath); } catch { /* best effort */ }
            return null;
        }

        return item;
    }

    /// <summary>
    /// Runs the OCR + extraction + schema-validation pipeline for a stored file.
    /// Never throws for expected failures — outcomes are reported on the <see cref="ProcessResponse"/>.
    /// </summary>
    public async Task<ProcessResponse> ProcessAsync(Guid id, string ownerKey, CancellationToken cancellationToken)
    {
        var item = _store.Get(ownerKey, id);
        if (item is null)
        {
            return new ProcessResponse { Success = false, Message = "سند یافت نشد." };
        }

        item.Status = IntakeFileStatus.Processing;
        item.Errors.Clear();
        item.SchemaIssues.Clear();

        var filePath = Path.Combine(_environment.WebRootPath, item.StoredRelativePath);
        byte[] fileBytes;
        try
        {
            if (!File.Exists(filePath))
            {
                item.Status = IntakeFileStatus.Failed;
                item.Errors.Add("فایل روی دیسک یافت نشد.");
                return Fail(item);
            }

            fileBytes = await File.ReadAllBytesAsync(filePath, cancellationToken);
        }
        catch (Exception ex)
        {
            item.Status = IntakeFileStatus.Failed;
            item.Errors.Add($"خطا در خواندن فایل: {ex.Message}");
            return Fail(item);
        }

        if (item.IsPdf)
        {
            // PDF rasterization (scanned-PDF OCR) is a follow-up phase; ingest is supported,
            // but automatic text extraction is not yet available for raw PDFs.
            item.Status = IntakeFileStatus.Failed;
            item.RawOcrText = null;
            item.OcrConfidence = 0;
            item.Errors.Add("استخراج متن از فایل PDF نیازمند تبدیل به تصویر است؛ لطفاً فیلدها را بهصورت دستی تکمیل کنید.");
            item.SchemaIssues = _schemaValidator.Validate(InvoiceJsonSchemaValidator.ToDocument(null)).ToList();
            return Fail(item);
        }

        if (!item.IsImagePreviewable)
        {
            item.Status = IntakeFileStatus.Failed;
            item.Errors.Add("قالب فایل برای پردازش OCR پشتیبانی نمیشود.");
            return Fail(item);
        }

        OcrResult ocr;
        try
        {
            ocr = await _ocrEngine.RecognizeAsync(fileBytes, language: null, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OCR pipeline threw an unexpected exception for intake item {IntakeId}", id);
            item.Status = IntakeFileStatus.Failed;
            item.Errors.Add("پردازش OCR با خطای غیرمنتظره مواجه شد.");
            return Fail(item);
        }

        if (!ocr.IsSuccess)
        {
            item.Status = IntakeFileStatus.Failed;
            item.OcrConfidence = 0;
            item.Errors.Add(string.IsNullOrWhiteSpace(ocr.ErrorMessage) ? "پردازش OCR ناموفق بود." : ocr.ErrorMessage!);
            return Fail(item);
        }

        if (!ocr.HasMeaningfulText)
        {
            item.Status = IntakeFileStatus.Failed;
            item.Errors.Add("متنی از تصویر استخراج نشد. لطفاً تصویر واضحتری بارگذاری کنید یا فیلدها را دستی وارد کنید.");
            return Fail(item);
        }

        // Normalize and extract.
        item.RawOcrText = PersianTextParser.Normalize(ocr.RawText);
        item.OcrConfidence = Math.Round(ocr.MeanConfidence, 1);
        item.Extracted = _fieldExtractor.Extract(item.RawOcrText);
        item.SchemaIssues = _schemaValidator.Validate(InvoiceJsonSchemaValidator.ToDocument(item.Extracted)).ToList();

        if (item.SchemaIssues.Count > 0)
        {
            item.Errors.Add("برخی فیلدهای الزامی استخراج یا معتبر نشدند؛ لطفاً پیش از ادامه، متادیتا را بررسی کنید.");
        }

        item.Status = IntakeFileStatus.Completed;

        return new ProcessResponse
        {
            Success = true,
            Message = item.SchemaIssues.Count == 0
                ? "فاکتور با موفقیت پردازش و اعتبارسنجی شد."
                : "پردازش انجام شد اما نیاز به بازبینی فیلدها دارد.",
            Item = ToDto(item)
        };
    }

    /// <summary>
    /// Applies user-corrected metadata to an intake item and re-runs schema validation.
    /// </summary>
    public ProcessResponse UpdateMetadata(Guid id, string ownerKey, UpdateMetadataRequest request)
    {
        var item = _store.Get(ownerKey, id);
        if (item is null)
        {
            return new ProcessResponse { Success = false, Message = "سند یافت نشد." };
        }

        var extracted = new ExtractedInvoice
        {
            InvoiceNumber = (request.InvoiceNumber ?? string.Empty).Trim(),
            DateShamsi = (request.DateShamsi ?? string.Empty).Trim(),
            TotalAmount = request.TotalAmount,
            SellerName = (request.SellerName ?? string.Empty).Trim(),
            BuyerName = (request.BuyerName ?? string.Empty).Trim()
        };

        // Parse the Shamsi date entered by the user into a Gregorian value when possible.
        var parsedDate = PersianTextParser.TryParseDate(extracted.DateShamsi);
        extracted.Date = parsedDate;
        if (parsedDate is { } dt && string.IsNullOrWhiteSpace(extracted.DateShamsi))
        {
            extracted.DateShamsi = PersianTextParser.ToShamsiDateString(dt);
        }

        item.Extracted = extracted;
        item.SchemaIssues = _schemaValidator.Validate(InvoiceJsonSchemaValidator.ToDocument(extracted)).ToList();
        item.Errors.Clear();
        if (item.SchemaIssues.Count > 0)
        {
            item.Errors.Add("برخی فیلدهای الزامی معتبر نیستند؛ لطفاً مقادیر را بررسی کنید.");
        }

        // Make a previously failed file usable again once its required fields are valid.
        if (item.Status == IntakeFileStatus.Failed && item.SchemaIssues.Count == 0)
        {
            item.Status = IntakeFileStatus.Completed;
        }

        return new ProcessResponse
        {
            Success = item.SchemaIssues.Count == 0,
            Message = item.SchemaIssues.Count == 0
                ? "متادیتا با موفقیت ذخیره شد."
                : "متادیتا ذخیره شد اما فیلدهای الزامی ناقص هستند.",
            Item = ToDto(item)
        };
    }

    public bool Delete(Guid id, string ownerKey)
    {
        var item = _store.Get(ownerKey, id);
        if (item is null)
        {
            return false;
        }

        _store.Remove(ownerKey, id);

        var filePath = Path.Combine(_environment.WebRootPath, item.StoredRelativePath);
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not delete stored intake file {Path}", filePath);
        }

        return true;
    }

    public IReadOnlyList<InvoiceIntakeItem> List(string ownerKey) => _store.GetAll(ownerKey);

    public string BuildPreviewUrl(InvoiceIntakeItem item) =>
        "/" + item.StoredRelativePath.Replace('\\', '/');

    public InvoiceIntakeItemDto ToDto(InvoiceIntakeItem item) =>
        InvoiceIntakeItemDto.From(item, BuildPreviewUrl(item));

    private static ProcessResponse Fail(InvoiceIntakeItem item) =>
        new() { Success = false, Message = item.Errors.LastOrDefault() ?? "پردازش ناموفق بود." };

    private static string SanitizeOwnerKey(string ownerKey)
    {
        var safe = new string(
            (ownerKey ?? "anonymous")
                .Where(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_')
                .ToArray());
        return string.IsNullOrWhiteSpace(safe) ? "anonymous" : safe;
    }
}

