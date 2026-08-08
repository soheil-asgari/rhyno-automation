using System.Text.Json.Serialization;

namespace OfficeAutomation.Modules.MowadiyanIntegration;

/// <summary>
/// Official Mowadiyan (سامانه مودیان) invoice payload structure.
/// The Iranian Tax Portal accepts a JSON representation of an invoice with
/// header, body (line items), and an optional extension block.
/// </summary>
public sealed class MowadiyanInvoicePayload
{
    [JsonPropertyName("Header")]
    public MowadiyanHeader Header { get; set; } = new();

    [JsonPropertyName("Body")]
    public MowadiyanBody Body { get; set; } = new();

    [JsonPropertyName("Extension")]
    public MowadiyanExtension? Extension { get; set; }

    public string ToJson() => System.Text.Json.JsonSerializer.Serialize(
        this,
        new System.Text.Json.JsonSerializerOptions
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = null,
            WriteIndented = false
        });
}

/// <summary>Invoice header metadata.</summary>
public sealed class MowadiyanHeader
{
    /// <summary>Invoice number (شماره فاکتور).</summary>
    [JsonPropertyName("InvoiceNumber")]
    public string InvoiceNumber { get; set; } = string.Empty;

    /// <summary>Shamsi date of issuance (yyyy/MM/dd).</summary>
    [JsonPropertyName("Date")]
    public string Date { get; set; } = string.Empty;

    /// <summary>Invoice type code (e.g. 01 = sale, 02 = purchase).</summary>
    [JsonPropertyName("InvoiceType")]
    public string InvoiceType { get; set; } = "01";

    /// <summary>Total amount before tax (ریال).</summary>
    [JsonPropertyName("TotalAmount")]
    public decimal TotalAmount { get; set; }

    /// <summary>Total discount (ریال).</summary>
    [JsonPropertyName("TotalDiscount")]
    public decimal TotalDiscount { get; set; }

    /// <summary>VAT amount (مالیات بر ارزش افزوده) in ریال.</summary>
    [JsonPropertyName("VatAmount")]
    public decimal VatAmount { get; set; }

    /// <summary>Duty / Avarez amount (عوارض) in ریال.</summary>
    [JsonPropertyName("DutyAmount")]
    public decimal DutyAmount { get; set; }

    /// <summary>Final payable amount (ریال).</summary>
    [JsonPropertyName("FinalAmount")]
    public decimal FinalAmount { get; set; }

    /// <summary>VAT rate applied (e.g. 0.10).</summary>
    [JsonPropertyName("VatRate")]
    public decimal VatRate { get; set; } = 0.10m;

    /// <summary>Seller economic code (کد اقتصادی فروشنده).</summary>
    [JsonPropertyName("SellerEconomicCode")]
    public string? SellerEconomicCode { get; set; }

    /// <summary>Seller name.</summary>
    [JsonPropertyName("SellerName")]
    public string? SellerName { get; set; }

    /// <summary>Buyer economic code (کد اقتصادی خریدار).</summary>
    [JsonPropertyName("BuyerEconomicCode")]
    public string? BuyerEconomicCode { get; set; }

    /// <summary>Buyer national ID (شناسه ملی خریدار).</summary>
    [JsonPropertyName("BuyerNationalId")]
    public string? BuyerNationalId { get; set; }

    /// <summary>Buyer name.</summary>
    [JsonPropertyName("BuyerName")]
    public string? BuyerName { get; set; }
}

/// <summary>Invoice body — collection of line items.</summary>
public sealed class MowadiyanBody
{
    [JsonPropertyName("LineItems")]
    public List<MowadiyanLineItem> LineItems { get; set; } = new();
}

/// <summary>A single line item.</summary>
public sealed class MowadiyanLineItem
{
    /// <summary>Sequence number of the line.</summary>
    [JsonPropertyName("LineNumber")]
    public int LineNumber { get; set; }

    /// <summary>Description of the item/service.</summary>
    [JsonPropertyName("Description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>Quantity.</summary>
    [JsonPropertyName("Quantity")]
    public decimal Quantity { get; set; }

    /// <summary>Unit price (ریال).</summary>
    [JsonPropertyName("UnitPrice")]
    public decimal UnitPrice { get; set; }

    /// <summary>Line amount (قبل از مالیات) in ریال.</summary>
    [JsonPropertyName("LineAmount")]
    public decimal LineAmount { get; set; }

    /// <summary>Kind of line (Item, Discount, Tax, Duty, Shipping, Insurance).</summary>
    [JsonPropertyName("Kind")]
    public string Kind { get; set; } = "Item";

    /// <summary>Whether the line is tax exempt.</summary>
    [JsonPropertyName("IsTaxExempt")]
    public bool IsTaxExempt { get; set; }
}

/// <summary>Optional extension block.</summary>
public sealed class MowadiyanExtension
{
    [JsonPropertyName("Source")]
    public string? Source { get; set; }

    [JsonPropertyName("Notes")]
    public string? Notes { get; set; }

    [JsonPropertyName("InternalId")]
    public string? InternalId { get; set; }
}