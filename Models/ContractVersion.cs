using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OfficeAutomation.Models;

/// <summary>
/// ذخیره‌سازی نسخه‌ای از قرارداد — اسنپشات کامل فیلدها در لحظه ذخیره
/// major: برای اصلاحیه‌ها (amendment) — minor: برای ویرایش‌های جزئی
/// </summary>
public class ContractVersion
{
    public int Id { get; set; }

    [Required]
    public int ContractId { get; set; }

    public Contract? Contract { get; set; }

    /// <summary>نسخه اصلی — با هر اصلاحیه/تمدید یک واحد افزایش می‌یابد</summary>
    public int MajorVersion { get; set; } = 1;

    /// <summary>نسخه فرعی — با هر ویرایش جزئی یک واحد افزایش می‌یابد</summary>
    public int MinorVersion { get; set; } = 0;

    /// <summary>رشته نسخه نمایشی (مثلاً "1.2")</summary>
    [StringLength(20)]
    public string VersionLabel => $"{MajorVersion}.{MinorVersion}";

    /// <summary>اسنپشات JSON از تمام فیلدهای ردیابی‌شده</summary>
    [Required]
    public string SnapshotData { get; set; } = "{}";

    /// <summary>کاربر ایجادکننده نسخه</summary>
    [Required]
    [StringLength(450)]
    public string CreatedByUserId { get; set; } = string.Empty;

    public User? CreatedByUser { get; set; }

    /// <summary>توضیح تغییرات این نسخه</summary>
    [StringLength(2000)]
    public string? ChangeDescription { get; set; }

    /// <summary>نوع رویداد: Created | Edited | Amendment | StatusChange | BulkUpdate</summary>
    [StringLength(30)]
    public string ChangeType { get; set; } = "Edited";

    /// <summary>تاریخ ثبت نسخه</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// نتیجه مقایسه دو نسخه — لیست فیلدهای تغییر کرده با مقادیر قدیم و جدید
/// </summary>
public class VersionDiffEntry
{
    public string FieldName { get; set; } = string.Empty;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public bool IsSignificant { get; set; } // تغییرات مالی/تاریخی مهم
}

/// <summary>
/// فیلدهایی که در اسنپشات نسخه ردیابی می‌شوند
/// </summary>
public class ContractSnapshot
{
    public string ContractNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? ContractType { get; set; }
    public int? CustomerId { get; set; }
    public string? ClientName { get; set; }
    public string? ClientPhone { get; set; }
    public string? ClientAddress { get; set; }
    public string? Description { get; set; }
    public string? Notes { get; set; }
    public string? TermsAndConditions { get; set; }
    public string? ContractBodyHtml { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal RemainingBalance { get; set; }
    public string PaymentStatus { get; set; } = "Unpaid";
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime? SignDate { get; set; }
    public int ExpiryNoticeDays { get; set; } = 30;
    public string Status { get; set; } = "Draft";
    public string? AssignedUserId { get; set; }
    public int? TemplateId { get; set; }
    public bool IsArchived { get; set; }

    /// <summary>برنامه زمان‌بندی پرداخت (nullable — در نسخه‌های قدیمی وجود ندارد)</summary>
    public PaymentScheduleSnapshot? PaymentSchedule { get; set; }
}

/// <summary>
/// فیلدهای برنامه پرداخت در اسنپشات نسخه
/// </summary>
public class PaymentScheduleSnapshot
{
    public string ScheduleType { get; set; } = string.Empty;
    public int InstallmentCount { get; set; }
    public decimal TotalAmount { get; set; }
    public List<InstallmentSnapshotLine> Installments { get; set; } = new();
}

public class InstallmentSnapshotLine
{
    public int InstallmentNumber { get; set; }
    public DateTime DueDate { get; set; }
    public decimal Amount { get; set; }
    public decimal? Percentage { get; set; }
    public string? Description { get; set; }
    public string Status { get; set; } = "Pending";
    public decimal PaidAmount { get; set; }
}
