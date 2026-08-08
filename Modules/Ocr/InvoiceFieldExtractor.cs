using System.Text.RegularExpressions;

namespace OfficeAutomation.Modules.Ocr;

/// <summary>
/// Extracts the key invoice business fields from normalized OCR text using robust
/// Persian + English label patterns.
/// </summary>
public sealed partial class InvoiceFieldExtractor
{
    /// <summary>
    /// Extracts invoice fields from raw OCR text. All outputs are normalized and parsed.
    /// </summary>
    public ExtractedInvoice Extract(string? rawText)
    {
        var result = ExtractedInvoice.Empty();
        if (string.IsNullOrWhiteSpace(rawText))
        {
            return result;
        }

        var normalized = PersianTextParser.Normalize(rawText);

        result.InvoiceNumber = ExtractInvoiceNumber(normalized);
        result.SellerName = ExtractParty(normalized, SellerNamePattern());
        result.BuyerName = ExtractParty(normalized, BuyerNamePattern());
        result.TotalAmount = ParseTotal(normalized);

        var dateString = ExtractDateString(normalized);
        if (!string.IsNullOrEmpty(dateString))
        {
            var parsed = PersianTextParser.TryParseDate(dateString);
            result.Date = parsed;
            result.DateShamsi = parsed is { } dt ? PersianTextParser.ToShamsiDateString(dt) : dateString;
        }

        return result;
    }

    private static string ExtractInvoiceNumber(string normalized)
    {
        // "شماره فاکتور: ۱۲۳۴۵" or "Invoice No: 12345" or bare "فاکتور شماره: 12".
        foreach (var pattern in InvoiceNumberPatterns())
        {
            var match = pattern.Match(normalized);
            if (match.Success && !string.IsNullOrWhiteSpace(match.Groups["num"].Value))
            {
                return PersianTextParser.ToLatinDigits(match.Groups["num"].Value.Trim());
            }
        }

        return string.Empty;
    }

    private static string ExtractParty(string normalized, Regex labels)
    {
        var match = labels.Match(normalized);
        if (!match.Success)
        {
            return string.Empty;
        }

        var value = string.IsNullOrWhiteSpace(match.Groups["name"].Value)
            ? match.Groups["value"].Value
            : match.Groups["name"].Value;

        return value.Trim().Trim(':').Trim();
    }

    private static decimal? ParseTotal(string normalized)
    {
        foreach (var pattern in TotalAmountPatterns())
        {
            var match = pattern.Match(normalized);
            if (!match.Success)
            {
                continue;
            }

            // Reconstruct the sentence containing the amount so units (تومان) are recognized.
            var start = Math.Max(0, match.Index);
            var length = Math.Min(normalized.Length - start, 90);
            var sentence = normalized.Substring(start, length);
            return PersianTextParser.TryParseAmount(sentence);
        }

        return null;
    }

    private static string ExtractDateString(string normalized)
    {
        foreach (var pattern in DatePatterns())
        {
            var match = pattern.Match(normalized);
            if (match.Success)
            {
                var value = match.Groups["d"].Value;
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return PersianTextParser.ToLatinDigits(value);
                }
            }
        }

        return string.Empty;
    }

    private static IEnumerable<Regex> InvoiceNumberPatterns()
    {
        yield return LabeledValuePattern("(?:شماره\\s*فاکتور|فاکتور\\s*شماره|شماره)");
        yield return LabeledValuePattern("(?:invoice\\s*(?:no|number|num)|no\\.?|inv\\.?\\s*[:#])");
        yield return LabeledValuePattern("(?:سریال)");
        // Bare alpha-numeric invoice code like INV-00123 or ۱۴۰۳-۱۲۳۴.
        yield return new Regex(@"\b(?:INV|IV|FV)[-\s]?[A-Za-z0-9\u06F0-\u06F9]{2,20}\b", RegexOptions.IgnoreCase);
    }

    private static IEnumerable<Regex> TotalAmountPatterns()
    {
        yield return LabeledValuePattern("(?:جمع\\s*کل|جمع\\s*فاکتور|مبلغ\\s*کل|مبلغ\\s*قابل\\s*پرداخت|جمع\\s*نهایی|مبلغ\\s*نهایی|مانده|بدهی|قابل\\s*پرداخت)");
        yield return LabeledValuePattern("(?:grand\\s*total|total\\s*amount|amount\\s*due|total|net\\s*amount)");
        yield return new Regex(@"(?<num>\d{1,3}(?:[.,٬]\d{3})*(?:\.\d{1,2})?)\s*(?:ریال|تومان)?", RegexOptions.IgnoreCase);
    }

    private static IEnumerable<Regex> DatePatterns()
    {
        yield return LabeledValuePattern("(?:تاریخ\\s*(?:فاکتور|صدور|سند)?)");
        yield return LabeledValuePattern("(?:date|invoice\\s*date|issued\\s*date)");
        // Bare Shamsi/Gregorian date.
        yield return new Regex(@"(?<d>\d{4}\s*(?:/|-|\.)\s*\d{1,2}\s*(?:/|-|\.)\s*\d{1,2})", RegexOptions.IgnoreCase);
    }

    private static Regex LabeledValuePattern(string label) =>
        new(
            $@"{label}\s*[:：]?\s*(?<num>[0-9\u06F0-\u06F9][0-9\u06F0-\u06F9\s/.\-:]{{1,40}})",
            RegexOptions.IgnoreCase);

    private static Regex SellerNamePattern() =>
        new(@"(?:فروشنده|صادرکننده|تامین\s*کننده|seller|vendor|supplier|from)\s*[:：]?\s*(?<name>[^\r\n،;]{2,80})", RegexOptions.IgnoreCase);

    private static Regex BuyerNamePattern() =>
        new(@"(?:خریدار|مشتری|گیرنده|buyer|customer|to)\s*[:：]?\s*(?<name>[^\r\n،;]{2,80})", RegexOptions.IgnoreCase);
}
