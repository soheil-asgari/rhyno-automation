using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OfficeAutomation.Models
{
    /// <summary>
    /// یک درخواست تایید برای قرارداد — می‌تواند یک یا چند مرحله تایید داشته باشد
    /// (supporting sequential AND parallel approval chains).
    /// </summary>
    public class ContractApprovalRequest
    {
        public int Id { get; set; }

        [Required]
        public int ContractId { get; set; }

        public Contract? Contract { get; set; }

        /// <summary>شناسه کاربری که درخواست را ایجاد کرده است</summary>
        [Required]
        [StringLength(450)]
        public string RequestedByUserId { get; set; } = string.Empty;

        public User? RequestedByUser { get; set; }

        /// <summary>
        /// وضعیت کلی درخواست: Pending, Approved, Rejected, PartiallyApproved, Cancelled
        /// </summary>
        [Required]
        [StringLength(30)]
        public string Status { get; set; } = ApprovalRequestStatus.Pending;

        /// <summary>الگوی تایید: Sequential یا Parallel</summary>
        [Required]
        [StringLength(20)]
        public string ApprovalMode { get; set; } = OfficeAutomation.Models.ApprovalMode.Sequential;

        /// <summary>مبلغ قرارداد در زمان ارسال (برای احراز threshold)</summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal SubmittedAmount { get; set; }

        /// <summary>توضیح درخواست‌دهنده</summary>
        [StringLength(2000)]
        public string? Description { get; set; }

        /// <summary>نتیجه نهایی (در صورت تایید/رد نهایی)</summary>
        [StringLength(1000)]
        public string? FinalComment { get; set; }

        /// <summary>در صورت رد شدن، قرارداد به کدام وضعیت برگردد: Draft یا PreviousStage</summary>
        [StringLength(20)]
        public string? RejectionReturnMode { get; set; }

        /// <summary>شماره مرحله فعلی (برای زنجیره ترتیبی)</summary>
        public int CurrentStepNumber { get; set; }

        /// <summary>هشدار انقضا — مهلت تایید به ساعت</summary>
        public int? ExpiryHours { get; set; }

        public DateTime? ExpiresAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? CompletedAt { get; set; }

        // ── Navigation ──
        public List<ContractApprovalStep> Steps { get; set; } = new();
    }

    /// <summary>
    /// ثابت‌های وضعیت درخواست تایید
    /// </summary>
    public static class ApprovalRequestStatus
    {
        public const string Pending = "Pending";
        public const string PartiallyApproved = "PartiallyApproved";
        public const string Approved = "Approved";
        public const string Rejected = "Rejected";
        public const string Cancelled = "Cancelled";

        public static readonly string[] All = [Pending, PartiallyApproved, Approved, Rejected, Cancelled];
    }

    /// <summary>
    /// ثابت‌های حالت تایید (زنجیره‌ای یا موازی)
    /// </summary>
    public static class ApprovalMode
    {
        public const string Sequential = "Sequential";
        public const string Parallel = "Parallel";

        public static readonly string[] All = [Sequential, Parallel];
    }

    /// <summary>
    /// ثابت‌های تصمیم در هر مرحله
    /// </summary>
    public static class ApprovalStepDecision
    {
        public const string Pending = "Pending";
        public const string Approved = "Approved";
        public const string Rejected = "Rejected";
        public const string Skipped = "Skipped";
        public const string Delegated = "Delegated";

        public static readonly string[] All = [Pending, Approved, Rejected, Skipped, Delegated];
    }
}