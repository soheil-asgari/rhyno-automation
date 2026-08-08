using System.Text.RegularExpressions;

namespace OfficeAutomation.Modules.InvoiceStructuring;

/// <summary>
/// Analyzes OCR-extracted line-item text to differentiate between
/// actual item amounts, discounts (تخفیف), and tax/VAT (مالیات بر ارزش افزوده).
/// </summary>
public static partial class ItemizedTaxClassifier
{
    public enum LineItemKind
    {
        Item,
        Discount,
        Tax,
        Duty,
        Shipping,
        Insurance,
        Unknown
    }

    public sealed class ClassifiedLineItem
    {
        public string Description { get; init; } = string.Empty;
        public LineItemKind Kind { get; init; } = LineItemKind.Unknown;
        public decimal? Amount { get; init; }
        public decimal? Quantity { get; init; }
        public decimal? UnitPrice { get; init; }
        public bool IsTaxExempt { get; init; }
        public string RawText { get; init; } = string.Empty;
    }

    public sealed class ClassifiedSummary
    {
        public List<ClassifiedLineItem> Items { get; init; } = new();
        public List<ClassifiedLineItem> Discounts { get; init; } = new();
        public List<ClassifiedLineItem> TaxLines { get; init; } = new();
        public List<ClassifiedLineItem> DutyLines { get; init; } = new();
        public List<ClassifiedLineItem> ShippingLines { get; init; } = new();
        public List<ClassifiedLineItem> InsuranceLines { get; init; } = new();
        public List<ClassifiedLineItem> UnknownLines { get; init; } = new();

        public decimal TotalItemAmount => Items.Sum(i => i.Amount ?? 0);
        public decimal TotalDiscount => Discounts.Sum(d => d.Amount ?? 0);
        public decimal TotalTax => TaxLines.Sum(t => t.Amount ?? 0);
        public decimal TotalDuty => DutyLines.Sum(d => d.Amount ?? 0);
        public decimal TotalShipping => ShippingLines.Sum(s => s.Amount ?? 0);
        public decimal TotalInsurance => InsuranceLines.Sum(i => i.Amount ?? 0);
    }
    public static ClassifiedLineItem Classify(string lineText, decimal? amount = null)
    {
        if (string.IsNullOrWhiteSpace(lineText))
        {
            return new ClassifiedLineItem { RawText = lineText ?? string.Empty, Kind = LineItemKind.Unknown };
        }

        var normalized = PersianDigitsToLatin(lineText.Trim());
        var kind = DetermineKind(normalized);
        var isExempt = IsExemptItem(normalized);

        return new ClassifiedLineItem
        {
            Description = ExtractDescription(normalized),
            Kind = kind,
            Amount = amount,
            IsTaxExempt = isExempt,
            RawText = lineText
        };
    }

    public static ClassifiedSummary ClassifyBatch(IEnumerable<string> lines)
    {
        var summary = new ClassifiedSummary();

        foreach (var line in lines)
        {
            var amount = ExtractAmount(line);
            var classified = Classify(line, amount);

            switch (classified.Kind)
            {
                case LineItemKind.Item: summary.Items.Add(classified); break;
                case LineItemKind.Discount: summary.Discounts.Add(classified); break;
                case LineItemKind.Tax: summary.TaxLines.Add(classified); break;
                case LineItemKind.Duty: summary.DutyLines.Add(classified); break;
                case LineItemKind.Shipping: summary.ShippingLines.Add(classified); break;
                case LineItemKind.Insurance: summary.InsuranceLines.Add(classified); break;
                default: summary.UnknownLines.Add(classified); break;
            }
        }

        return summary;
    }

    public static bool IsExemptItem(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        return ExemptPattern().IsMatch(text);
    }

    public static decimal? ExtractAmount(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var normalized = PersianDigitsToLatin(text);
        var match = AmountPattern().Match(normalized);
        if (!match.Success) return null;
        var raw = match.Groups["num"].Value.Replace(",", "").Replace("٬", "").Trim();
        if (decimal.TryParse(raw, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var amount))
            return amount;
        return null;
    }

    private static LineItemKind DetermineKind(string normalized)
    {
        if (TaxPattern().IsMatch(normalized)) return LineItemKind.Tax;
        if (DutyPattern().IsMatch(normalized)) return LineItemKind.Duty;
        if (DiscountPattern().IsMatch(normalized)) return LineItemKind.Discount;
        if (ShippingPattern().IsMatch(normalized)) return LineItemKind.Shipping;
        if (InsurancePattern().IsMatch(normalized)) return LineItemKind.Insurance;
        return LineItemKind.Item;
    }

    private static string ExtractDescription(string normalized)
    {
        var cleaned = LabelStripPattern().Replace(normalized, "").Trim();
        cleaned = Regex.Replace(cleaned, @"\s+", " ");
        return string.IsNullOrWhiteSpace(cleaned) ? normalized : cleaned;
    }

    private static string PersianDigitsToLatin(string input)
    {
        return input
            .Replace('۰', '0').Replace('۱', '1').Replace('۲', '2')
            .Replace('۳', '3').Replace('۴', '4').Replace('۵', '5')
            .Replace('۶', '6').Replace('۷', '7').Replace('۸', '8')
            .Replace('۹', '9')
            .Replace('٠', '0').Replace('١', '1').Replace('٢', '2')
            .Replace('٣', '3').Replace('٤', '4').Replace('٥', '5')
            .Replace('٦', '6').Replace('٧', '7').Replace('٨', '8')
            .Replace('٩', '9');
    }

    [GeneratedRegex(@"(?<num>\d{1,3}(?:[.,٬]\d{3})*(?:\.\d{1,2})?)")]
    private static partial Regex AmountPattern();

    [GeneratedRegex(@"\b(?:مالیات|مالیات\s*بر\s*ارزش\s*افزوده|vat|value\s*added\s*tax)\b", RegexOptions.IgnoreCase)]
    private static partial Regex TaxPattern();

    [GeneratedRegex(@"\b(?:عوارض|duty|avarez)\b", RegexOptions.IgnoreCase)]
    private static partial Regex DutyPattern();

    [GeneratedRegex(@"\b(?:تخفیف|discount|تخفيف)\b", RegexOptions.IgnoreCase)]
    private static partial Regex DiscountPattern();

    [GeneratedRegex(@"\b(?:حمل\s*و\s*نقل|باربری|پست|shipping|freight|delivery)\b", RegexOptions.IgnoreCase)]
    private static partial Regex ShippingPattern();

    [GeneratedRegex(@"\b(?:بیمه|insurance)\b", RegexOptions.IgnoreCase)]
    private static partial Regex InsurancePattern();

    [GeneratedRegex(@"\b(?:معاف|معافیت|exempt|tax[- ]?free)\b", RegexOptions.IgnoreCase)]
    private static partial Regex ExemptPattern();

    [GeneratedRegex(@"^(?:کالا|شرح|محصول|خدمت|item|desc|product|service)\s*[:：]?\s*", RegexOptions.IgnoreCase)]
    private static partial Regex LabelStripPattern();
}

