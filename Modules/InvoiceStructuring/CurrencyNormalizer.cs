using System.Globalization;
using System.Text.RegularExpressions;

namespace OfficeAutomation.Modules.InvoiceStructuring;

/// <summary>
/// Auto-detects and converts between Toman (تومان) and Rial (ریال) based on
/// extracted amounts and text context. All internal storage uses Rial.
/// 1 Toman = 10 Rial.
/// </summary>
public static partial class CurrencyNormalizer
{
    private const decimal TomanToRialMultiplier = 10m;
    private const decimal RialToTomanDivisor = 10m;

    /// <summary>
    /// Normalizes a monetary amount to Rial. If the textual context indicates Toman,
    /// the amount is multiplied by 10.
    /// </summary>
    /// <param name="amount">Raw amount (may be in Toman or Rial).</param>
    /// <param name="contextText">Surrounding text for unit detection.</param>
    /// <returns>Amount in Rial.</returns>
    public static decimal NormalizeToRial(decimal amount, string? contextText)
    {
        if (IsTomanContext(contextText))
        {
            return amount * TomanToRialMultiplier;
        }

        return amount;
    }

    /// <summary>
    /// Converts a Rial amount to Toman for display.
    /// </summary>
    public static decimal ToToman(decimal rialAmount) => rialAmount / RialToTomanDivisor;

    /// <summary>
    /// Detects whether the surrounding text indicates the amount is in Toman.
    /// </summary>
    public static bool IsTomanContext(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return TomanPattern().IsMatch(text) && !RialPattern().IsMatch(text);
    }

    /// <summary>
    /// Detects whether the surrounding text indicates the amount is in Rial.
    /// </summary>
    public static bool IsRialContext(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return RialPattern().IsMatch(text);
    }

    /// <summary>
    /// Formats a Rial amount with Persian digits and appropriate suffix.
    /// </summary>
    public static string FormatRial(decimal rialAmount)
    {
        return $"{ToPersianDigits(rialAmount.ToString("N0", CultureInfo.InvariantCulture))} ریال";
    }

    /// <summary>
    /// Formats a Toman amount with Persian digits and appropriate suffix.
    /// </summary>
    public static string FormatToman(decimal rialAmount)
    {
        var toman = ToToman(rialAmount);
        return $"{ToPersianDigits(toman.ToString("N0", CultureInfo.InvariantCulture))} تومان";
    }

    /// <summary>
    /// Converts Latin digits (0-9) to Persian digits (۰-۹).
    /// </summary>
    public static string ToPersianDigits(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }

        return input
            .Replace('0', '۰')
            .Replace('1', '۱')
            .Replace('2', '۲')
            .Replace('3', '۳')
            .Replace('4', '۴')
            .Replace('5', '۵')
            .Replace('6', '۶')
            .Replace('7', '۷')
            .Replace('8', '۸')
            .Replace('9', '۹');
    }

    [GeneratedRegex(@"\bتومان\b", RegexOptions.IgnoreCase)]
    private static partial Regex TomanPattern();

    [GeneratedRegex(@"\bریال\b", RegexOptions.IgnoreCase)]
    private static partial Regex RialPattern();
}
