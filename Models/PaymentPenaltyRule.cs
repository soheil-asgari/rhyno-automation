using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OfficeAutomation.Models;

/// <summary>
/// قانون جریمه دیرکرد پرداخت برای یک قرارداد
/// </summary>
public class PaymentPenaltyRule
{
    public int Id { get; set; }

    [Required]
    public int ContractId { get; set; }

    [ForeignKey(nameof(ContractId))]
    public Contract? Contract { get; set; }

    /// <summary>تعداد روزهای تنفس после سررسید قبل از اعمال جریمه</summary>
    [Display(Name = "روزهای تنفس")]
    [Range(0, 365)]
    public int GracePeriodDays { get; set; } = 0;

    /// <summary>
    /// نوع جریمه: Fixed, PercentagePerDay, PercentagePerWeek, PercentagePerMonth, Compound
    /// </summary>
    [Required]
    [StringLength(30)]
    [Display(Name = "نوع جریمه")]
    public string PenaltyType { get; set; } = PenaltyTypeNames.PercentagePerMonth;

    /// <summary>مبلغ ثابت جریمه (برای Fixed)</summary>
    [Column(TypeName = "decimal(18,2)")]
    [Display(Name = "مبلغ جریمه")]
    [Range(0, 999999999999)]
    public decimal? PenaltyAmount { get; set; }

    /// <summary>درصد جریمه (برای انواع درصدی)</summary>
    [Column(TypeName = "decimal(5,2)")]
    [Display(Name = "درصد جریمه")]
    [Range(0, 100)]
    public decimal? PenaltyPercentage { get; set; }

    /// <summary>
    /// سقف جریمه (AbsoluteAmount = مبلغ ثابت، PercentageOfContract = درصد از مبلغ قرارداد)
    /// </summary>
    [Required]
    [StringLength(20)]
    [Display(Name = "نوع سقف")]
    public string CapType { get; set; } = CapTypeNames.None;

    /// <summary>مبلغ/درصد سقف</summary>
    [Column(TypeName = "decimal(18,2)")]
    [Display(Name = "مقدار سقف")]
    [Range(0, 999999999999)]
    public decimal? CapValue { get; set; }

    /// <summary>فعال بودن قانون</summary>
    [Display(Name = "فعال")]
    public bool IsActive { get; set; } = true;

    /// <summary>تاریخ ایجاد</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>آخرین ویرایش</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// ثابت‌های نوع جریمه
/// </summary>
public static class PenaltyTypeNames
{
    public const string Fixed = "Fixed";
    public const string PercentagePerDay = "PercentagePerDay";
    public const string PercentagePerWeek = "PercentagePerWeek";
    public const string PercentagePerMonth = "PercentagePerMonth";
    public const string Compound = "Compound";

    public static readonly string[] All = [Fixed, PercentagePerDay, PercentagePerWeek, PercentagePerMonth, Compound];

    public static readonly Dictionary<string, string> DisplayNames = new()
    {
        [Fixed] = "مبلغ ثابت",
        [PercentagePerDay] = "درصد روزانه",
        [PercentagePerWeek] = "درصد هفتگی",
        [PercentagePerMonth] = "درصد ماهانه",
        [Compound] = "مرکب"
    };
}

/// <summary>
/// ثابت‌های نوع سقف جریمه
/// </summary>
public static class CapTypeNames
{
    public const string None = "None";
    public const string AbsoluteAmount = "AbsoluteAmount";
    public const string PercentageOfContract = "PercentageOfContract";

    public static readonly string[] All = [None, AbsoluteAmount, PercentageOfContract];

    public static readonly Dictionary<string, string> DisplayNames = new()
    {
        [None] = "بدون سقف",
        [AbsoluteAmount] = "مبلغ ثابت",
        [PercentageOfContract] = "درصد از قرارداد"
    };
}

/// <summary>
/// جریمه محاسبه شده برای یک پرداخت دیرکرد
/// </summary>
public class CalculatedPenalty
{
    public int Id { get; set; }

    [Required]
    public int PaymentId { get; set; }

    [ForeignKey(nameof(PaymentId))]
    public ContractPayment? Payment { get; set; }

    [Required]
    public int ContractId { get; set; }

    [ForeignKey(nameof(ContractId))]
    public Contract? Contract { get; set; }

    /// <summary>مبلغ محاسبه شده جریمه</summary>
    [Required]
    [Column(TypeName = "decimal(18,2)")]
    [Display(Name = "مبلغ جریمه")]
    public decimal CalculatedAmount { get; set; }

    /// <summary>تاریخ اعمال</summary>
    [DataType(DataType.Date)]
    [Display(Name = "تاریخ اعمال")]
    public DateTime AppliedDate { get; set; } = DateTime.Today;

    /// <summary>
    /// وضعیت: Pending, Applied, Waived
    /// </summary>
    [Required]
    [StringLength(20)]
    [Display(Name = "وضعیت")]
    public string Status { get; set; } = PenaltyStatusNames.Pending;

    /// <summary>شرح نحوه محاسبه</summary>
    [StringLength(1000)]
    [Display(Name = "مبنای محاسبه")]
    public string? CalculationBasis { get; set; }

    /// <summary>تاریخ ایجاد</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// ثابت‌های وضعیت جریمه
/// </summary>
public static class PenaltyStatusNames
{
    public const string Pending = "Pending";
    public const string Applied = "Applied";
    public const string Waived = "Waived";

    public static readonly string[] All = [Pending, Applied, Waived];

    public static readonly Dictionary<string, string> DisplayNames = new()
    {
        [Pending] = "در انتظار",
        [Applied] = "اعمال شده",
        [Waived] = "بخشوده شده"
    };

    public static readonly Dictionary<string, string> BadgeColors = new()
    {
        [Pending] = "bg-amber-100 text-amber-800 border-amber-200 dark:bg-amber-900/30 dark:text-amber-400 dark:border-amber-800/30",
        [Applied] = "bg-rose-100 text-rose-800 border-rose-200 dark:bg-rose-900/30 dark:text-rose-400 dark:border-rose-800/30",
        [Waived] = "bg-slate-100 text-slate-800 border-slate-200 dark:bg-slate-900/30 dark:text-slate-400 dark:border-slate-800/30"
    };
}