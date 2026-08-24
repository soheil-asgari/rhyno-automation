using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OfficeAutomation.Models;

/// <summary>
/// اصلاحیه / الحاقیه / تمدید قرارداد
/// Amendment workflow: Draft → PendingApproval → Approved → Applied (or Rejected/Cancelled)
/// </summary>
public class ContractAmendment
{
    public int Id { get; set; }

    /// <summary>قرارداد والد</summary>
    [Required]
    public int ContractId { get; set; }

    public Contract? Contract { get; set; }

    /// <summary>
    /// نوع: "Amendment" (اصلاحیه), "Addendum" (الحاقیه), "Renewal" (تمدید)
    /// </summary>
    [Required]
    [StringLength(20)]
    [Display(Name = "نوع")]
    public string AmendmentType { get; set; } = AmendmentTypeNames.Amendment;

    /// <summary>شماره اصلاحیه (نسخه انسانی: 1, 2, 3 …)</summary>
    [Display(Name = "شماره اصلاحیه")]
    public int AmendmentNumber { get; set; }

    /// <summary>عنوان</summary>
    [Required]
    [StringLength(200)]
    [Display(Name = "عنوان")]
    public string Title { get; set; } = string.Empty;

    /// <summary>شرح تغییرات</summary>
    [StringLength(4000)]
    [Display(Name = "شرح")]
    public string? Description { get; set; }

    // ── Financial Impact ──

    /// <summary>تغییر در مبلغ کل (مثبت = افزایش، منفی = کاهش)</summary>
    [Column(TypeName = "decimal(18,2)")]
    [Display(Name = "تغییر مبلغ")]
    public decimal AmountChange { get; set; }

    /// <summary>مبلغ جدید قرارداد پس از اعمال اصلاحیه</summary>
    [Column(TypeName = "decimal(18,2)")]
    [Display(Name = "مبلغ جدید")]
    public decimal? NewTotalAmount { get; set; }

    /// <summary>تغییر در تاریخ پایان (تعداد روز)</summary>
    [Display(Name = "تغییر تاریخ پایان (روز)")]
    public int? EndDateChangeDays { get; set; }

    /// <summary>تاریخ پایان جدید</summary>
    [DataType(DataType.Date)]
    [Display(Name = "تاریخ پایان جدید")]
    public DateTime? NewEndDate { get; set; }

    /// <summary>تاریخ اثرگذاری اصلاحیه</summary>
    [Required]
    [DataType(DataType.Date)]
    [Display(Name = "تاریخ اثر")]
    public DateTime EffectiveDate { get; set; } = DateTime.Today;

    // ── Workflow Status ──

    /// <summary>
    /// وضعیت گردش کار: Draft, PendingApproval, Approved, Applied, Rejected, Cancelled
    /// </summary>
    [Required]
    [StringLength(20)]
    [Display(Name = "وضعیت")]
    public string Status { get; set; } = AmendmentStatus.Draft;

    /// <summary>درخواست تایید مرتبط (از Item 1)</summary>
    public int? ApprovalRequestId { get; set; }

    [ForeignKey(nameof(ApprovalRequestId))]
    public ContractApprovalRequest? ApprovalRequest { get; set; }

    /// <summary>نسخه قرارداد ایجاد شده پس از اعمال (از Item 5)</summary>
    public int? VersionSnapshotId { get; set; }

    // ── Auditing ──

    [Required]
    [StringLength(450)]
    public string CreatedByUserId { get; set; } = string.Empty;

    public User? CreatedByUser { get; set; }

    [StringLength(450)]
    public string? AppliedByUserId { get; set; }

    public User? AppliedByUser { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? AppliedAt { get; set; }

    // ── Multi-Currency (inherited from parent contract if null) ──
    /// <summary>کد ارز اصلاحیه (اگر خالی باشد، از ارز قرارداد استفاده می‌شود)</summary>
    [StringLength(3)]
    [Display(Name = "ارز اصلاحیه")]
    public string? CurrencyCode { get; set; }

    /// <summary>نرخ تبدیل به ارز پایه</summary>
    [Column(TypeName = "decimal(18,6)")]
    [Display(Name = "نرخ تبدیل")]
    public decimal? ExchangeRate { get; set; }

    /// <summary>نرخ مالیات اصلاحیه (در صورت تغییر)</summary>
    [Column(TypeName = "decimal(5,2)")]
    [Display(Name = "نرخ مالیات اصلاحیه (%)")]
    public decimal? TaxRatePercent { get; set; }
}

/// <summary>
/// ثابت‌های نوع اصلاحیه
/// </summary>
public static class AmendmentTypeNames
{
    public const string Amendment = "Amendment";
    public const string Addendum = "Addendum";
    public const string Renewal = "Renewal";

    public static readonly string[] All = [Amendment, Addendum, Renewal];
}

/// <summary>
/// ثابت‌های وضعیت اصلاحیه
/// </summary>
public static class AmendmentStatus
{
    public const string Draft = "Draft";
    public const string PendingApproval = "PendingApproval";
    public const string Approved = "Approved";
    public const string Applied = "Applied";
    public const string Rejected = "Rejected";
    public const string Cancelled = "Cancelled";

    public static readonly string[] All = [Draft, PendingApproval, Approved, Applied, Rejected, Cancelled];
}