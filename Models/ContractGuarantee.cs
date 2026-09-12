using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OfficeAutomation.Models;

/// <summary>
/// ضمانت‌نامه / تضمین / کسر وجه تضمین (Bank Guarantee / Retention Money)
/// </summary>
public class ContractGuarantee
{
    public int Id { get; set; }

    /// <summary>قرارداد والد</summary>
    [Required]
    public int ContractId { get; set; }

    public Contract? Contract { get; set; }

    /// <summary>اصلاحیه مرتبط (اختیاری)</summary>
    public int? AmendmentId { get; set; }

    [ForeignKey(nameof(AmendmentId))]
    public ContractAmendment? Amendment { get; set; }

    /// <summary>
    /// نوع ضمانت: BankGuarantee, RetentionMoney, ParentCompanyGuarantee, PerformanceBond, AdvancePaymentGuarantee, WarrantyGuarantee, BidBond, Other
    /// </summary>
    [Required]
    [StringLength(40)]
    [Display(Name = "نوع ضمانت")]
    public string GuaranteeType { get; set; } = GuaranteeTypeNames.BankGuarantee;

    /// <summary>شماره ضمانت‌نامه</summary>
    [Required]
    [StringLength(100)]
    [Display(Name = "شماره ضمانت")]
    public string GuaranteeNumber { get; set; } = string.Empty;

    /// <summary>بانک/صادرکننده</summary>
    [StringLength(200)]
    [Display(Name = "بانک / صادرکننده")]
    public string? IssuingBank { get; set; }

    /// <summary>مبلغ ضمانت</summary>
    [Column(TypeName = "decimal(18,2)")]
    [Display(Name = "مبلغ")]
    public decimal Amount { get; set; }

    /// <summary>تاریخ صدور</summary>
    [Required]
    [DataType(DataType.Date)]
    [Display(Name = "تاریخ صدور")]
    public DateTime IssuedDate { get; set; } = DateTime.Today;

    /// <summary>تاریخ انقضا</summary>
    [Required]
    [DataType(DataType.Date)]
    [Display(Name = "تاریخ انقضا")]
    public DateTime ExpiryDate { get; set; }

    /// <summary>
    /// وضعیت: Active, Expiring, Released, Claimed, Expired
    /// </summary>
    [Required]
    [StringLength(20)]
    [Display(Name = "وضعیت")]
    public string Status { get; set; } = GuaranteeStatusNames.Active;

    /// <summary>درصد کسر وجه تضمین (برای RetentionMoney)</summary>
    [Column(TypeName = "decimal(5,2)")]
    [Display(Name = "درصد کسر وجه")]
    public decimal? RetentionPercentage { get; set; }

    /// <summary>شرایط آزادسازی</summary>
    [StringLength(500)]
    [Display(Name = "شرایط آزادسازی")]
    public string? ReleaseCondition { get; set; }

    /// <summary>تاریخ آزادسازی</summary>
    [DataType(DataType.Date)]
    [Display(Name = "تاریخ آزادسازی")]
    public DateTime? ReleasedDate { get; set; }

    // ── Auditing ──

    [Required]
    [StringLength(450)]
    public string CreatedByUserId { get; set; } = string.Empty;

    public User? CreatedByUser { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// ثابت‌های نوع ضمانت
/// </summary>
public static class GuaranteeTypeNames
{
    public const string BankGuarantee = "BankGuarantee";
    public const string RetentionMoney = "RetentionMoney";
    public const string ParentCompanyGuarantee = "ParentCompanyGuarantee";
    public const string PerformanceBond = "PerformanceBond";
    public const string AdvancePaymentGuarantee = "AdvancePaymentGuarantee";
    public const string WarrantyGuarantee = "WarrantyGuarantee";
    public const string BidBond = "BidBond";
    public const string Other = "Other";

    public static readonly string[] All = [BankGuarantee, RetentionMoney, ParentCompanyGuarantee, PerformanceBond, AdvancePaymentGuarantee, WarrantyGuarantee, BidBond, Other];

    public static readonly Dictionary<string, string> DisplayNames = new()
    {
        [BankGuarantee] = "ضمانت‌نامه بانکی",
        [RetentionMoney] = "کسور وجه تضمین",
        [ParentCompanyGuarantee] = "ضمانت شرکت مادر",
        [PerformanceBond] = "ضمانت حسن اجرا",
        [AdvancePaymentGuarantee] = "ضمانت پیش‌پرداخت",
        [WarrantyGuarantee] = "ضمانت گارانتی",
        [BidBond] = "ضمانت شرکت در مناقصه",
        [Other] = "سایر"
    };
}

/// <summary>
/// ثابت‌های وضعیت ضمانت
/// </summary>
public static class GuaranteeStatusNames
{
    public const string Active = "Active";
    public const string Expiring = "Expiring";
    public const string Released = "Released";
    public const string Claimed = "Claimed";
    public const string Expired = "Expired";

    public static readonly string[] All = [Active, Expiring, Released, Claimed, Expired];

    public static readonly Dictionary<string, string> DisplayNames = new()
    {
        [Active] = "فعال",
        [Expiring] = "در شرف انقضا",
        [Released] = "آزاد شده",
        [Claimed] = "مطالبه شده",
        [Expired] = "منقضی شده"
    };

    public static readonly Dictionary<string, string> BadgeColors = new()
    {
        [Active] = "bg-emerald-100 text-emerald-800 border-emerald-200 dark:bg-emerald-950/40 dark:text-emerald-300 dark:border-emerald-800/40",
        [Expiring] = "bg-amber-100 text-amber-800 border-amber-200 dark:bg-amber-950/40 dark:text-amber-300 dark:border-amber-800/40",
        [Released] = "bg-blue-100 text-blue-800 border-blue-200 dark:bg-blue-950/40 dark:text-blue-300 dark:border-blue-800/40",
        [Claimed] = "bg-red-100 text-red-800 border-red-200 dark:bg-rose-950/40 dark:text-rose-300 dark:border-rose-800/40",
        [Expired] = "bg-gray-100 text-gray-800 border-gray-200 dark:bg-slate-800 dark:text-slate-300 dark:border-slate-700"
    };
}