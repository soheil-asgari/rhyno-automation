using System.IO.Compression;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using OfficeAutomation.Modules.InvoiceIntake;

namespace OfficeAutomation.Modules.IntakeAutomation;

/// <summary>
/// Extracts supported invoice files from a bulk ZIP upload and feeds each entry
/// directly into <see cref="InvoiceIntakeService"/> so they flow through the same
/// validation, OCR and schema pipeline as individually uploaded files.
/// </summary>
public sealed class ZipArchiveProcessor
{
    public const int DefaultMaxEntries = 200;
    public const long DefaultMaxZipBytes = 100L * 1024 * 1024;

    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".pdf"
    };

    private readonly InvoiceIntakeService _intakeService;
    private readonly ILogger<ZipArchiveProcessor> _logger;

    public ZipArchiveProcessor(
        InvoiceIntakeService intakeService,
        ILogger<ZipArchiveProcessor> logger)
    {
        _intakeService = intakeService;
        _logger = logger;
    }

    /// <summary>
    /// Extracts a ZIP stream and auto-queues each supported entry into the intake
    /// pipeline. Returns a summary of stored / rejected / processed entries.
    /// </summary>
    public async Task<ZipArchiveProcessResult> ProcessZipAsync(
        Stream zipStream,
        string ownerKey,
        CancellationToken cancellationToken,
        int maxEntries = DefaultMaxEntries,
        long maxZipBytes = DefaultMaxZipBytes)
    {
        var result = new ZipArchiveProcessResult();

        if (zipStream is null)
        {
            result.Success = false;
            result.Message = "جریان صفر (null) ZIP دریافت شد.";
            return result;
        }

        if (zipStream.Length > maxZipBytes)
        {
            result.Success = false;
            result.Message = $"حجم فایل ZIP بیش از حد مجاز ({maxZipBytes / (1024 * 1024)} مگابایت) است.";
            return result;
        }

        if (zipStream.Length <= 0)
        {
            result.Success = false;
            result.Message = "فایل ZIP خالی است.";
            return result;
        }

        try
        {
            using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read, leaveOpen: true);

            var storedIds = new List<Guid>();
            var entries = archive.Entries;

            if (entries.Count == 0)
            {
                result.Success = true;
                result.Message = "آرشیو ZIP حاوی هیچ فایلی نیست.";
                return result;
            }

            if (entries.Count > maxEntries)
            {
                result.Success = false;
                result.Message = $"تعداد فایل‌های داخل ZIP بیش از حد مجاز ({maxEntries}) است.";
                return result;
            }

            foreach (var entry in entries)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (string.IsNullOrWhiteSpace(entry.Name))
                {
                    continue; // skip directory entries
                }

                var extension = Path.GetExtension(entry.Name);
                if (!SupportedExtensions.Contains(extension))
                {
                    result.RejectedCount++;
                    result.Errors.Add($"رد شد (پسوند نامعتبر): {entry.Name}");
                    continue;
                }

                if (entry.Length > _intakeService.MaxFileBytes)
                {
                    result.RejectedCount++;
                    result.Errors.Add($"رد شد (حجم فایل بیش از حد مجاز): {entry.Name}");
                    continue;
                }

                var formFile = await ToFormFileAsync(entry, cancellationToken);
                if (formFile is null)
                {
                    result.RejectedCount++;
                    result.Errors.Add($"رد شد (خطا در خواندن): {entry.Name}");
                    continue;
                }

                var item = _intakeService.ValidateAndStore(formFile, ownerKey, out var error);
                if (item is null)
                {
                    result.RejectedCount++;
                    result.Errors.Add($"{entry.Name}: {error}");
                    // Explicitly release the wrapper stream to avoid leaking file handles.
                    await formFile.OpenReadStream().DisposeAsync();
                    continue;
                }

                storedIds.Add(item.Id);
                result.StoredCount++;
                result.Items.Add(_intakeService.ToDto(item));

                // Auto-queue: run the OCR/extraction pipeline for the stored entry.
                var process = await _intakeService.ProcessAsync(item.Id, ownerKey, cancellationToken);
                if (process.Success)
                {
                    result.ProcessedCount++;
                }
            }
result.Success = result.StoredCount > 0;
            result.Message = result.Success
                ? $"{result.StoredCount} فایل از ZIP بارگذاری و {result.ProcessedCount} فایل پردازش شد."
                : "هیچ فایل قابل پردازشی از ZIP استخراج نشد.";

            _logger.LogInformation(
                "ZIP intake complete. Stored={Stored}, Processed={Processed}, Rejected={Rejected}",
                result.StoredCount, result.ProcessedCount, result.RejectedCount);

            return result;
        }
        catch (InvalidDataException ex)
        {
            _logger.LogWarning(ex, "Invalid ZIP archive submitted for intake.");
            result.Success = false;
            result.Message = "فایل ZIP معتبر نیست و قابل استخراج نمی‌باشد.";
            return result;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while processing ZIP archive for owner {Owner}", ownerKey);
            result.Success = false;
            result.Message = $"خطا در پردازش فایل ZIP: {ex.Message}";
            return result;
        }
    }

    private static async Task<FormFile?> ToFormFileAsync(
        ZipArchiveEntry entry,
        CancellationToken cancellationToken)
    {
        try
        {
            var stream = new MemoryStream();
            await using (var source = entry.Open())
            {
                await source.CopyToAsync(stream, cancellationToken);
            }

            stream.Position = 0;
            var formFile = new FormFile(stream, 0, stream.Length, "files", entry.Name)
            {
                Headers = new HeaderDictionary(),
                ContentType = ResolveContentType(Path.GetExtension(entry.Name))
            };
            return formFile;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string ResolveContentType(string extension) => extension.ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".pdf" => "application/pdf",
        _ => "application/octet-stream"
    };
}