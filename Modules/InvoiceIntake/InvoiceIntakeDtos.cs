using OfficeAutomation.Modules.Ocr;

namespace OfficeAutomation.Modules.InvoiceIntake;

/// <summary>A single JSON schema validation issue (JSON-Pointer + message).</summary>
public sealed record InvoiceSchemaIssue(string Path, string Message);

/// <summary>Serializable representation of an intake item returned to the UI.</summary>
public sealed class InvoiceIntakeItemDto
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string Extension { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsImagePreviewable { get; set; }
    public bool IsPdf { get; set; }
    public double OcrConfidence { get; set; }
    public string PreviewUrl { get; set; } = string.Empty;
    public string? RawOcrText { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<InvoiceSchemaIssue> SchemaIssues { get; set; } = new();
    public ExtractedInvoiceFieldsDto? Fields { get; set; }

    public static InvoiceIntakeItemDto From(InvoiceIntakeItem item, string previewUrl)
    {
        return new InvoiceIntakeItemDto
        {
            Id = item.Id,
            FileName = item.OriginalFileName,
            Extension = item.Extension,
            ContentType = item.ContentType,
            FileSize = item.FileSize,
            Status = item.Status.ToString(),
            IsImagePreviewable = item.IsImagePreviewable,
            IsPdf = item.IsPdf,
            OcrConfidence = item.OcrConfidence,
            PreviewUrl = previewUrl,
            RawOcrText = item.RawOcrText,
            Errors = item.Errors,
            SchemaIssues = item.SchemaIssues,
            Fields = item.Extracted is { } extracted
                ? new ExtractedInvoiceFieldsDto
                {
                    InvoiceNumber = extracted.InvoiceNumber,
                    DateShamsi = extracted.DateShamsi,
                    Date = extracted.Date,
                    TotalAmount = extracted.TotalAmount,
                    SellerName = extracted.SellerName,
                    BuyerName = extracted.BuyerName
                }
                : null
        };
    }
}

/// <summary>Editable invoice fields used by the metadata editor and returned to the UI.</summary>
public sealed class ExtractedInvoiceFieldsDto
{
    public string InvoiceNumber { get; set; } = string.Empty;
    public string DateShamsi { get; set; } = string.Empty;
    public DateTime? Date { get; set; }
    public decimal? TotalAmount { get; set; }
    public string SellerName { get; set; } = string.Empty;
    public string BuyerName { get; set; } = string.Empty;

    public static ExtractedInvoiceFieldsDto From(ExtractedInvoice source) =>
        new()
        {
            InvoiceNumber = source.InvoiceNumber,
            DateShamsi = source.DateShamsi,
            Date = source.Date,
            TotalAmount = source.TotalAmount,
            SellerName = source.SellerName,
            BuyerName = source.BuyerName
        };

    public ExtractedInvoice ToExtracted() =>
        new()
        {
            InvoiceNumber = InvoiceNumber,
            DateShamsi = DateShamsi,
            Date = Date,
            TotalAmount = TotalAmount,
            SellerName = SellerName,
            BuyerName = BuyerName
        };
}

/// <summary>Request body for updating file metadata before processing.</summary>
public sealed class UpdateMetadataRequest
{
    public string InvoiceNumber { get; set; } = string.Empty;
    public string DateShamsi { get; set; } = string.Empty;
    public decimal? TotalAmount { get; set; }
    public string SellerName { get; set; } = string.Empty;
    public string BuyerName { get; set; } = string.Empty;
}

/// <summary>Result of processing a single file through the OCR + validation pipeline.</summary>
public sealed class ProcessResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public InvoiceIntakeItemDto? Item { get; set; }
}
