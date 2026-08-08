using System.Globalization;

namespace OfficeAutomation.Modules.InvoiceStructuring;

/// <summary>
/// Validates and normalizes Shamsi (Jalali) dates, parsing them to
/// Gregorian <see cref="DateTime"/> for SQL storage and providing
/// round-trip Shamsi string formatting.
/// </summary>
public static class JalaliDateHelper
{
    private static readonly PersianCalendar Persian = new();

    /// <summary>
    /// Validates that the given year, month, and day form a valid Shamsi date.
    /// </summary>
    public static bool IsValidShamsiDate(int year, int month, int day)
    {
        if (year < 1 || year > 9999 || month < 1 || month > 12 || day < 1 || day > 31)
        {
            return false;
        }

        // Shamsi months: 1-6 have 31 days, 7-11 have 30 days, 12 has 29 (or 30 in leap).
        int maxDay = month switch
        {
            >= 1 and <= 6 => 31,
            >= 7 and <= 11 => 30,
            12 => Persian.IsLeapYear(year) ? 30 : 29,
            _ => 31
        };

        return day <= maxDay;
    }

    /// <summary>
    /// Parses a Shamsi date string (yyyy/MM/dd or yyyy-MM-dd or yyyy.MM.dd)
    /// and returns the equivalent Gregorian <see cref="DateTime"/>.
    /// Returns null if the input is invalid or unparseable.
    /// </summary>
    public static DateTime? ParseShamsiToGregorian(string? shamsiDate)
    {
        if (string.IsNullOrWhiteSpace(shamsiDate))
        {
            return null;
        }

        // Normalize Persian digits to Latin.
        var normalized = PersianDigitsToLatin(shamsiDate.Trim());

        // Accept / - . as separators.
        var parts = normalized.Split('/', '-', '.');
        if (parts.Length != 3)
        {
            return null;
        }

        if (!int.TryParse(parts[0], out var year) ||
            !int.TryParse(parts[1], out var month) ||
            !int.TryParse(parts[2], out var day))
        {
            return null;
        }

        if (!IsValidShamsiDate(year, month, day))
        {
            return null;
        }

        try
        {
            return Persian.ToDateTime(year, month, day, 0, 0, 0, 0);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    /// <summary>
    /// Converts a Gregorian <see cref="DateTime"/> to a Shamsi date string (yyyy/MM/dd).
    /// </summary>
    public static string ToShamsiString(DateTime gregorianDate)
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            "{0:0000}/{1:00}/{2:00}",
            Persian.GetYear(gregorianDate),
            Persian.GetMonth(gregorianDate),
            Persian.GetDayOfMonth(gregorianDate));
    }

    /// <summary>
    /// Converts a Gregorian <see cref="DateTime"/> to a Shamsi date string with Persian digits.
    /// </summary>
    public static string ToShamsiStringPersianDigits(DateTime gregorianDate)
    {
        var latin = ToShamsiString(gregorianDate);
        return LatinToPersianDigits(latin);
    }

    /// <summary>
    /// Returns today's date as a Shamsi string.
    /// </summary>
    public static string TodayShamsi() => ToShamsiString(DateTime.Now);

    /// <summary>
    /// Returns the current Shamsi year.
    /// </summary>
    public static int CurrentShamsiYear() => Persian.GetYear(DateTime.Now);

    /// <summary>
    /// Validates a Shamsi date range. Returns true if from &lt;= to.
    /// </summary>
    public static bool IsValidDateRange(string? fromShamsi, string? toShamsi)
    {
        var from = ParseShamsiToGregorian(fromShamsi);
        var to = ParseShamsiToGregorian(toShamsi);

        if (from is null || to is null)
        {
            return false;
        }

        return from.Value <= to.Value;
    }

    /// <summary>
    /// Normalizes a Shamsi date string: trims, converts Persian digits to Latin,
    /// and ensures standard yyyy/MM/dd separator format.
    /// </summary>
    public static string NormalizeShamsiDateString(string? shamsiDate)
    {
        if (string.IsNullOrWhiteSpace(shamsiDate))
        {
            return string.Empty;
        }

        var normalized = PersianDigitsToLatin(shamsiDate.Trim());
        normalized = normalized.Replace('-', '/').Replace('.', '/');

        // Remove extra whitespace.
        normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"\s+", "");

        return normalized;
    }

    /// <summary>
    /// Converts Persian/Arabic-Indic digits to Latin digits.
    /// </summary>
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

    /// <summary>
    /// Converts Latin digits to Persian digits.
    /// </summary>
    private static string LatinToPersianDigits(string input)
    {
        return input
            .Replace('0', '۰').Replace('1', '۱').Replace('2', '۲')
            .Replace('3', '۳').Replace('4', '۴').Replace('5', '۵')
            .Replace('6', '۶').Replace('7', '۷').Replace('8', '۸')
            .Replace('9', '۹');
    }
}
