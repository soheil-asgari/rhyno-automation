using OfficeAutomation.Modules.InvoiceIntake;

namespace OfficeAutomation.Modules.IntakeAutomation;

/// <summary>
/// Outcome of a bulk ZIP archive ingestion operation.
/// </summary>
public sealed class ZipArchiveProcessResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;

    /// <summary>Number of archive entries successfully stored for intake.</summary>
    public int StoredCount { get; set; }

    /// <summary>Number of entries that were rejected/ignored.</summary>
    public int RejectedCount { get; set; }

    /// <summary>Number of stored entries that also completed OCR processing.</summary>
    public int ProcessedCount { get; set; }

    public List<string> Errors { get; set; } = new();
    public List<InvoiceIntakeItemDto> Items { get; set; } = new();
}

/// <summary>
/// A normalized incoming message from a messaging channel (Telegram / WhatsApp).
/// </summary>
public sealed class IncomingMediaMessage
{
    /// <summary>Source channel: "telegram" or "whatsapp".</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>Channel-provided unique message id.</summary>
    public string? MessageId { get; set; }

    /// <summary>Sender identifier (chat id / phone number).</summary>
    public string? SenderId { get; set; }

    /// <summary>Sender display name if provided.</summary>
    public string? SenderName { get; set; }

    /// <summary>Plain-text caption / message body.</summary>
    public string? Text { get; set; }

    /// <summary>File id(s) of attached media that can be downloaded for OCR.</summary>
    public List<string> MediaFileIds { get; set; } = new();

    /// <summary>MIME-like content type of the primary attachment, if known.</summary>
    public string? MediaContentType { get; set; }

    /// <summary>True when the message carries at least one downloadable media attachment.</summary>
    public bool HasMedia => MediaFileIds.Count > 0;
}

/// <summary>
/// Request contract for the secured webhook intake endpoint.
/// </summary>
public sealed class WebhookIntakeRequest
{
    /// <summary>Optional partition key used to scope the intake session.</summary>
    public string? OwnerKey { get; set; }

    /// <summary>Base64-encoded file when the connector cannot send multipart/form-data.</summary>
    public string? FileBase64 { get; set; }

    /// <summary>Original file name (e.g. "faktur.pdf").</summary>
    public string? FileName { get; set; }

    /// <summary>Optional metadata overrides applied before OCR.</summary>
    public WebhookIntakeMeta? Meta { get; set; }
}

/// <summary>Optional metadata overrides for webhook-delivered files.</summary>
public sealed class WebhookIntakeMeta
{
    public string? InvoiceNumber { get; set; }
    public string? DateShamsi { get; set; }
    public string? SellerName { get; set; }
    public string? BuyerName { get; set; }
    public decimal? TotalAmount { get; set; }
}