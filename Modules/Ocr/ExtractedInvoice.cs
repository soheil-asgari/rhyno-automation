using System.Globalization;

namespace OfficeAutomation.Modules.Ocr;

/// <summary>
/// The normalized, editable field set extracted from an invoice.
/// Mutable so users can review and correct OCR results before processing.
/// </summary>
public sealed class ExtractedInvoice
{
    public string InvoiceNumber { get; set; } = string.Empty;
    public string DateShamsi { get; set; } = string.Empty;
    public DateTime? Date { get; set; }
    public decimal? TotalAmount { get; set; }
    public string SellerName { get; set; } = string.Empty;
    public string BuyerName { get; set; } = string.Empty;

    public string ToJson() => System.Text.Json.JsonSerializer.Serialize(this);

    public static ExtractedInvoice Empty() => new();
}
