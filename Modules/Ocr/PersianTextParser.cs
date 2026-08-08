using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace OfficeAutomation.Modules.Ocr;

/// <summary>
/// Helpers for normalizing Persian/Arabic optical text and parsing Persian numbers and dates.
/// </summary>
public static partial class PersianTextParser
{
    // ۰-۹ (Persian) and ٠-٩ (Arabic-Indic) map to their ASCII counterparts.
    private static readonly Dictionary<char, char> DigitMap = BuildDigitMap();

    public static string Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(input.Length);
        foreach (var ch in input)
        {
            builder.Append(NormalizeChar(ch));
        }

        var normalized = builder.ToString();

        // Remove ZWNJ form half-spaces but keep the Persian spacing word separator.
        normalized = normalized.Replace('\u200c', ' ');

        return normalized.Trim();
    }

    public static string ToLatinDigits(string? input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(input.Length);
        foreach (var ch in input)
        {
            builder.Append(DigitMap.TryGetValue(ch, out var latin) ? latin : ch);
        }

        return builder.ToString();
    }

    /// <summary>
    /// Parses a monetary amount from text that may contain Persian digits, thousands separators
    /// and Persian unit suffixes (ریال / تومان). Returns null when no credible amount is found.
    /// </summary>
    public static decimal? TryParseAmount(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        var normalized = ToLatinDigits(input);

        // Extract the first plausible numeric expression (allow separators).
        var match = AmountPattern().Match(normalized);
        if (!match.Success)
        {
            return null;
        }

        var raw = match.Groups["num"].Value.Trim();
        if (string.IsNullOrEmpty(raw))
        {
            return null;
        }

        // Remove thousands separators that are not the decimal point.
        raw = raw.Replace(",", string.Empty).Replace("٬", string.Empty);
        raw = raw.TrimStart('.');

        if (!decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var amount))
        {
            return null;
        }

        var hasToman = TomanPattern().IsMatch(normalized);
        if (hasToman)
        {
            amount *= 10; // 1 تومان = 10 ریال
        }

        return amount;
    }

    /// <summary>
    /// Parses a date string into a Gregorian <see cref="DateTime"/>.
    /// Supports Shamsi (Jalali) formats such as ۱۴۰۳/۰۵/۱۲, 1403/05/12 and Gregorian dates.
    /// Returns null when the string cannot be interpreted as a valid date.
    /// </summary>
    public static DateTime? TryParseDate(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        var normalized = ToLatinDigits(input).Trim();

        // Handle Shamsi dates (first) since the module targets Persian invoices.
        var shamsi = ShamsiDatePattern().Match(normalized);
        if (shamsi.Success &&
            int.TryParse(shamsi.Groups["y"].Value, out var shY) &&
            int.TryParse(shamsi.Groups["m"].Value, out var shM) &&
            int.TryParse(shamsi.Groups["d"].Value, out var shD))
        {
            var persian = new PersianCalendar();
            try
            {
                return persian.ToDateTime(shY, shM, shD, 0, 0, 0, 0);
            }
            catch (ArgumentOutOfRangeException)
            {
                // Month/day out of Shamsi range; fall through.
            }
        }

        // Gregorian fallback.
        var gregorian = GregorianDatePattern().Match(normalized);
        if (gregorian.Success &&
            int.TryParse(gregorian.Groups["y"].Value, out var grY) &&
            int.TryParse(gregorian.Groups["m"].Value, out var grM) &&
            int.TryParse(gregorian.Groups["d"].Value, out var grD))
        {
            try
            {
                return new DateTime(grY, grM, grD);
            }
            catch (ArgumentOutOfRangeException)
            {
                return null;
            }
        }

        return null;
    }

    /// <summary>
    /// Converts a Gregorian date to its equivalent Shamsi calendar date string (yyyy/MM/dd).
    /// </summary>
    public static string ToShamsiDateString(DateTime date)
    {
        var persian = new PersianCalendar();
        return string.Format(
            CultureInfo.InvariantCulture,
            "{0:0000}/{1:00}/{2:00}",
            persian.GetYear(date),
            persian.GetMonth(date),
            persian.GetDayOfMonth(date));
    }
}

public static partial class PersianTextParser
{
    private static char NormalizeChar(char ch)
    {
        return ch switch
        {
            'ي' => 'ی',
            'ك' => 'ک',
            'ۀ' => 'ه',
            'ة' => 'ه',
            'أ' or 'إ' => 'ا',
            'ٱ' or 'آ' => 'ا',
            'ؤ' => 'و',
            'ى' => 'ی',
            _ => ch
        };
    }

    private static Dictionary<char, char> BuildDigitMap()
    {
        // ۰-۹ Persian and ٠-٩ Arabic-Indic digits → ASCII. Unknown chars are left intact by callers.
        return new Dictionary<char, char>
        {
            ['۰'] = '0', ['۱'] = '1', ['۲'] = '2', ['۳'] = '3', ['۴'] = '4',
            ['۵'] = '5', ['۶'] = '6', ['۷'] = '7', ['۸'] = '8', ['۹'] = '9',
            ['٠'] = '0', ['١'] = '1', ['٢'] = '2', ['٣'] = '3', ['٤'] = '4',
            ['٥'] = '5', ['٦'] = '6', ['٧'] = '7', ['٨'] = '8', ['٩'] = '9'
        };
    }

    [GeneratedRegex(@"(?<num>\d{1,3}(?:[.,٬]\d{3})*(?:\.\d{1,2})?|\d+(?:\.\d{1,2})?)")]
    private static partial Regex AmountPattern();

    [GeneratedRegex(@"تومان", RegexOptions.IgnoreCase)]
    private static partial Regex TomanPattern();

    [GeneratedRegex(@"(?<y>\d{4})\s*(?:/|-|\.)\s*(?<m>\d{1,2})\s*(?:/|-|\.)\s*(?<d>\d{1,2})")]
    private static partial Regex ShamsiDatePattern();

    [GeneratedRegex(@"(?<y>\d{4})[-/](?<m>\d{1,2})[-/](?<d>\d{1,2})")]
    private static partial Regex GregorianDatePattern();
}
