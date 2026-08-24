using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OfficeAutomation.Models;

/// <summary>
/// کد ارزهای پشتیبانی‌شده برای قراردادها و پرداخت‌ها
/// </summary>
public static class CurrencyCode
{
    public const string IRR = "IRR"; // ریال ایران
    public const string USD = "USD"; // دلار آمریکا
    public const string EUR = "EUR"; // یورو
    public const string GBP = "GBP"; // پوند
    public const string AED = "AED"; // درهم امارات
    public const string TRY = "TRY"; // لیر ترکیه

    public static readonly string[] All = [IRR, USD, EUR, GBP, AED, TRY];

    public static string GetSymbol(string currencyCode) => currencyCode switch
    {
        IRR => "﷼",
        USD => "$",
        EUR => "€",
        GBP => "£",
        AED => "د.إ",
        TRY => "₺",
        _ => currencyCode
    };

    public static bool IsSupported(string currencyCode) =>
        All.Contains(currencyCode);

    public static string GetDefault() => IRR;
}

/// <summary>
/// روش‌های محاسبه مالیات / ارزش افزوده
/// </summary>
public static class TaxCalculationMethod
{
    /// <summary>مالیات به مبلغ پایه اضافه می‌شود (رایج)</summary>
    public const string Exclusive = "Exclusive";

    /// <summary>مالیات در مبلغ پایه گنجانده شده است</summary>
    public const string Inclusive = "Inclusive";

    /// <summary>مالیات اعمال نمی‌شود</summary>
    public const string None = "None";

    public static readonly string[] All = [Exclusive, Inclusive, None];

    public static string GetDefault() => Exclusive;
}