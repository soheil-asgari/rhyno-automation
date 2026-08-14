namespace OfficeAutomation.Modules.Commercial.Domain;

/// <summary>
/// Lifecycle of a sales/purchase quotation.
/// </summary>
public static class CommercialQuotationStatus
{
    public const string Draft = "Draft";
    public const string Sent = "Sent";
    public const string Accepted = "Accepted";
    public const string Rejected = "Rejected";
    public const string Converted = "Converted";
    public const string Expired = "Expired";
    public const string Cancelled = "Cancelled";

    public static readonly string[] All =
    [
        Draft,
        Sent,
        Accepted,
        Rejected,
        Converted,
        Expired,
        Cancelled
    ];

    public static string Label(string? status)
    {
        return status switch
        {
            Draft => "پیش‌نویس",
            Sent => "ارسال شده",
            Accepted => "تایید شده",
            Rejected => "رد شده",
            Converted => "تبدیل به فاکتور",
            Expired => "منقضی شده",
            Cancelled => "لغو شده",
            _ => status ?? "-"
        };
    }

    public static string BadgeCss(string? status)
    {
        return status switch
        {
            Sent => "bg-sky-50 text-sky-700 border-sky-200/60 dark:bg-sky-950/40 dark:text-sky-400 dark:border-sky-800/40",
            Accepted or Converted => "bg-emerald-50 text-emerald-700 border-emerald-200/60 dark:bg-emerald-950/40 dark:text-emerald-400 dark:border-emerald-800/40",
            Rejected or Cancelled => "bg-rose-50 text-rose-700 border-rose-200/60 dark:bg-rose-950/40 dark:text-rose-400 dark:border-rose-800/40",
            Expired => "bg-amber-50 text-amber-700 border-amber-200/60 dark:bg-amber-950/40 dark:text-amber-400 dark:border-amber-800/40",
            _ => "bg-slate-100 text-slate-600 border-slate-200/60 dark:bg-slate-800/40 dark:text-slate-400 dark:border-slate-700/40"
        };
    }
}