using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OfficeAutomation.Models
{
    /// <summary>
    /// یک مرحله از زنجیره تایید قرارداد. هر مرحله می‌تواند به یک کاربر یا نقش اختصاص یابد.
    /// در حالت موازی (Parallel)، چندین مرحله با StepNumber یکسان همه باید تایید کنند.
    /// </summary>
    public class ContractApprovalStep
    {
        public int Id { get; set; }

        [Required]
        public int ContractApprovalRequestId { get; set; }

        public ContractApprovalRequest? ContractApprovalRequest { get; set; }

        /// <summary>شماره ترتیب مرحله (مراحل موازی شماره مشترک دارند)</summary>
        [Range(1, 100)]
        public int StepNumber { get; set; }

        /// <summary>برچسب مرحله مثلاً «مدیر مستقیم», «مدیر مالی», «مدیرعامل»</summary>
        [Required]
        [StringLength(100)]
        public string StepTitle { get; set; } = string.Empty;

        /// <summary>تامین‌کننده: User, Role, Threshold</summary>
        [Required]
        [StringLength(30)]
        public string AssigneeType { get; set; } = ApprovalStepAssigneeType.User;

        /// <summary>کاربر تاییدکننده (مستقیم)</summary>
        [StringLength(450)]
        public string? AssignedUserId { get; set; }

        public User? AssignedUser { get; set; }

        /// <summary>نقش تاییدکننده (برای تخصیص مبتنی بر نقش)</summary>
        [StringLength(450)]
        public string? AssignedRoleId { get; set; }

        /// <summary>حداقل آستانه مالی برای این مرحله (برای مسیریابی خودکار)</summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal? MinThreshold { get; set; }

        /// <summary>حداکثر آستانه مالی برای این مرحله</summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal? MaxThreshold { get; set; }

        /// <summary>وضعیت این مرحله: Pending, Approved, Rejected, Skipped, Delegated</summary>
        [Required]
        [StringLength(20)]
        public string Decision { get; set; } = ApprovalStepDecision.Pending;

        /// <summary>توضیح تاییدکننده (در صورت رد اجباری)</summary>
        [StringLength(2000)]
        public string? Comment { get; set; }

        /// <summary>در صورت رد شدن، مرحله به کدام مرحله بازگردد (null = draft)</summary>
        public int? ReturnToStepNumber { get; set; }

        /// <summary>کاربری که در نهایت تصمیم گرفته (ممکن است با AssignedUser متفاوت باشد در صورت Delegation)</summary>
        [StringLength(450)]
        public string? DecidedByUserId { get; set; }

        public User? DecidedByUser { get; set; }

        /// <summary>ارجاع داده شده به کاربر دیگر (Delegation)</summary>
        [StringLength(450)]
        public string? DelegatedToUserId { get; set; }

        public User? DelegatedToUser { get; set; }

        /// <summary>تاریخ تصمیم</summary>
        public DateTime? DecidedAt { get; set; }

        /// <summary>مهلت مرحله به ساعت</summary>
        public int? StepExpiryHours { get; set; }

        public DateTime? StepExpiresAt { get; set; }

        /// <summary>آیا این مرحله اجباری است یا اختیاری</summary>
        public bool IsRequired { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// ثابت‌های نوع تخصیص دهنده در مرحله تایید
    /// </summary>
    public static class ApprovalStepAssigneeType
    {
        public const string User = "User";
        public const string Role = "Role";
        public const string Threshold = "Threshold";

        public static readonly string[] All = [User, Role, Threshold];
    }
}