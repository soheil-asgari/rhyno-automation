using System.ComponentModel.DataAnnotations;

namespace OfficeAutomation.Models;

/// <summary>
/// الزامات مدارک برای هر نوع قرارداد
/// </summary>
public class DocumentRequirement
{
    public int Id { get; set; }

    /// <summary>نوع قرارداد (Lease, NDA, Sales, Service, …)</summary>
    [Required]
    [StringLength(60)]
    [Display(Name = "نوع قرارداد")]
    public string ContractType { get; set; } = string.Empty;

    /// <summary>دسته‌بندی مدرک (Certificate, Agreement, Identification, Insurance, …)</summary>
    [Required]
    [StringLength(60)]
    [Display(Name = "دسته‌بندی")]
    public string DocumentCategory { get; set; } = string.Empty;

    /// <summary>نام نمایشی مدرک به فارسی</summary>
    [Required]
    [StringLength(200)]
    [Display(Name = "نام مدرک")]
    public string DocumentName { get; set; } = string.Empty;

    /// <summary>الزامی بودن مدرک</summary>
    [Display(Name = "الزامی")]
    public bool IsRequired { get; set; } = true;

    /// <summary>توضیحات اختیاری</summary>
    [StringLength(500)]
    [Display(Name = "توضیحات")]
    public string? Description { get; set; }

    /// <summary>ترتیب نمایش</summary>
    [Display(Name = "ترتیب نمایش")]
    public int DisplayOrder { get; set; }
}

/// <summary>
/// ثابت‌های دسته‌بندی مدارک
/// </summary>
public static class DocumentRequirementCategories
{
    public const string Certificate = "Certificate";
    public const string Agreement = "Agreement";
    public const string Identification = "Identification";
    public const string Insurance = "Insurance";
    public const string Financial = "Financial";
    public const string Legal = "Legal";
    public const string Technical = "Technical";
    public const string Other = "Other";

    public static readonly string[] All = [Certificate, Agreement, Identification, Insurance, Financial, Legal, Technical, Other];

    public static readonly Dictionary<string, string> DisplayNames = new()
    {
        [Certificate] = "گواهی‌نامه",
        [Agreement] = "توافق‌نامه",
        [Identification] = "مدارک شناسایی",
        [Insurance] = "بیمه",
        [Financial] = "مدارک مالی",
        [Legal] = "مدارک حقوقی",
        [Technical] = "مدارک فنی",
        [Other] = "سایر"
    };
}

/// <summary>
/// وضعیت یک مدرک در checklist
/// </summary>
public static class DocumentStatus
{
    public const string Required = "Required";
    public const string Optional = "Optional";
    public const string Uploaded = "Uploaded";
    public const string Missing = "Missing";

    public static readonly Dictionary<string, string> BadgeClasses = new()
    {
        [Required] = "bg-amber-100 text-amber-800 border-amber-200 dark:bg-amber-900/30 dark:text-amber-400 dark:border-amber-800/30",
        [Optional] = "bg-slate-100 text-slate-600 border-slate-200 dark:bg-slate-800/30 dark:text-slate-400 dark:border-slate-700/30",
        [Uploaded] = "bg-emerald-100 text-emerald-700 border-emerald-200 dark:bg-emerald-900/30 dark:text-emerald-400 dark:border-emerald-800/30",
        [Missing] = "bg-rose-100 text-rose-700 border-rose-200 dark:bg-rose-900/30 dark:text-rose-400 dark:border-rose-800/30"
    };
}