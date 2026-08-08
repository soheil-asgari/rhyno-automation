using OfficeAutomation.Modules.Ocr;

namespace OfficeAutomation.Modules.InvoiceIntake;

/// <summary>
/// A single ingested invoice file together with its processing state and extracted fields.
/// </summary>
public sealed class InvoiceIntakeItem
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public string OriginalFileName { get; set; } = string.Empty;
    public string StoredFileName { get; set; } = string.Empty;
    public string StoredRelativePath { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
    public string Extension { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public DateTimeOffset UploadedAt { get; set; } = DateTimeOffset.Now;

    public IntakeFileStatus Status { get; set; } = IntakeFileStatus.Pending;

    /// <summary>Raw OCR text (normalized).</summary>
    public string? RawOcrText { get; set; }

    /// <summary>OCR mean confidence (0-100).</summary>
    public double OcrConfidence { get; set; }

    /// <summary>Extracted/editable invoice fields.</summary>
    public ExtractedInvoice? Extracted { get; set; }

    /// <summary>JSON schema validation results after processing.</summary>
    public List<InvoiceSchemaIssue> SchemaIssues { get; set; } = new();

    /// <summary>Human-readable processing/validation errors.</summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>True when an image is pre-renderable in the browser for preview.</summary>
    public bool IsImagePreviewable =>
        string.Equals(Extension, ".png", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(Extension, ".jpg", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(Extension, ".jpeg", StringComparison.OrdinalIgnoreCase);

    public bool IsPdf =>
        string.Equals(Extension, ".pdf", StringComparison.OrdinalIgnoreCase);

    public bool IsCompleted => Status == IntakeFileStatus.Completed;
}
