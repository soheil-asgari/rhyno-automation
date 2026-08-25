using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace OfficeAutomation.Models
{
    public static class ContractStatuses
    {
        public const string Draft = "پیش‌نویس";
        public const string PendingApproval = "در انتظار تایید";
        public const string Active = "فعال";
        public const string ExpiringSoon = "در شرف انقضا";
        public const string Suspended = "معلق";
        public const string Expired = "منقضی شده";
        public const string Completed = "تکمیل شده";
        public const string Cancelled = "لغو شده";
        public const string Terminated = "فسخ شده";
        public const string Archived = "بایگانی شده";

        public static readonly string[] All =
        [
            Draft,
            PendingApproval,
            Active,
            ExpiringSoon,
            Suspended,
            Expired,
            Completed,
            Cancelled,
            Terminated,
            Archived
        ];

        public static List<SelectListItem> GetStatusOptions()
        {
            return All.Select(s => new SelectListItem { Value = s, Text = s }).ToList();
        }
    }

    public static class ContractTypes
    {
        public const string Service = "خدمات";
        public const string Sales = "فروش";
        public const string Purchase = "خرید";
        public const string Lease = "اجاره";
        public const string Partnership = "مشارکت";
        public const string Maintenance = "نگهداری";
        public const string Consulting = "مشاوره";
        public const string NDA = "NDA";
        public const string Custom = "سفارشی";
        public const string Other = "سایر";

        public static readonly string[] All = [Service, Sales, Purchase, Lease, Partnership, Maintenance, Consulting, NDA, Custom, Other];

        public static List<SelectListItem> GetTypeOptions()
        {
            return All.Select(s => new SelectListItem { Value = s, Text = s }).ToList();
        }
    }

    public static class PaymentStatuses
    {
        public const string Unpaid = "Unpaid";
        public const string PartiallyPaid = "PartiallyPaid";
        public const string FullyPaid = "FullyPaid";

        public static readonly string[] All = [Unpaid, PartiallyPaid, FullyPaid];
    }

    public class Contract
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "شماره قرارداد الزامی است.")]
        [StringLength(60, ErrorMessage = "شماره قرارداد نمی‌تواند بیشتر از 60 کاراکتر باشد.")]
        [Display(Name = "شماره قرارداد")]
        public string ContractNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "عنوان قرارداد الزامی است.")]
        [StringLength(200, ErrorMessage = "عنوان قرارداد نمی‌تواند بیشتر از 200 کاراکتر باشد.")]
        [Display(Name = "عنوان قرارداد")]
        public string Title { get; set; } = string.Empty;

        /// <summary>نوع قرارداد</summary>
        [StringLength(60)]
        [Display(Name = "نوع قرارداد")]
        public string? ContractType { get; set; }

        // ── Customer Relationship (optional for standalone contracts) ──
        [Display(Name = "مشتری")]
        public int? CustomerId { get; set; }

        [Display(Name = "مشتری")]
        public Customer? Customer { get; set; }

        // ── Legacy free-text client fields (backward compatible) ──
        [Required(ErrorMessage = "نام طرف قرارداد الزامی است.")]
        [StringLength(150, ErrorMessage = "نام طرف قرارداد نمی‌تواند بیشتر از 150 کاراکتر باشد.")]
        [Display(Name = "طرف قرارداد")]
        public string ClientName { get; set; } = string.Empty;

        [StringLength(20)]
        [Display(Name = "تلفن طرف قرارداد")]
        public string? ClientPhone { get; set; }

        [StringLength(300)]
        [Display(Name = "آدرس طرف قرارداد")]
        public string? ClientAddress { get; set; }

        [StringLength(2000)]
        [Display(Name = "توضیحات")]
        public string? Description { get; set; }

        /// <summary>شرایط و ضوابط قرارداد</summary>
        [Display(Name = "شرایط و ضوابط")]
        public string? TermsAndConditions { get; set; }

        /// <summary>یادداشت‌های داخلی</summary>
        [Display(Name = "یادداشت‌ها")]
        public string? Notes { get; set; }

        // ── Financial ──
        [Required(ErrorMessage = "مبلغ کل قرارداد الزامی است.")]
        [Range(0, 999999999999, ErrorMessage = "مبلغ کل معتبر نیست.")]
        [Display(Name = "مبلغ کل")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }

        [Display(Name = "مبلغ پرداخت شده")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal PaidAmount { get; set; }

        /// <summary>باقی مانده (محاسبه خودکار)</summary>
        [Display(Name = "باقی مانده")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal RemainingBalance { get; set; }

        // ── Multi-Currency ──────────────────────────────────────────────
        /// <summary>کد ارز (IRR, USD, EUR, …)</summary>
        [Required]
        [StringLength(3)]
        [Display(Name = "ارز")]
        public string CurrencyCode { get; set; } = Models.CurrencyCode.IRR;

        /// <summary>نرخ تبدیل به ارز پایه (IRR) جهت گزارش‌گیری</summary>
        [Display(Name = "نرخ تبدیل")]
        [Column(TypeName = "decimal(18,6)")]
        public decimal ExchangeRate { get; set; } = 1m;

        /// <summary>مبلغ به ارز پایه (IRR) برای گزارش‌های مالی</summary>
        [Display(Name = "مبلغ پایه (IRR)")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal BaseCurrencyAmount { get; set; }

        // ── Tax / VAT ───────────────────────────────────────────────────
        /// <summary>روش محاسبه مالیات (Exclusive, Inclusive, None)</summary>
        [Required]
        [StringLength(20)]
        [Display(Name = "روش محاسبه مالیات")]
        public string TaxMethod { get; set; } = TaxCalculationMethod.Exclusive;

        /// <summary>نرخ مالیات پیش‌فرض (درصد)</summary>
        [Display(Name = "نرخ مالیات (%)")]
        [Column(TypeName = "decimal(5,2)")]
        public decimal DefaultTaxRatePercent { get; set; }

        /// <summary>مبلغ کل مالیات</summary>
        [Display(Name = "مجموع مالیات")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalTaxAmount { get; set; }

        /// <summary>مبلغ شامل مالیات (مبلغ کل + مالیات)</summary>
        [Display(Name = "مبلغ نهایی (با مالیات)")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmountInclusiveTax { get; set; }

        // ── Revenue Recognition (Accrual Basis) ──
        /// <summary>مبلغ درآمد شناسایی‌شده تاکنون منهای دریافت‌ها</summary>
        [Display(Name = "درآمد شناسایی‌شده")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal RecognizedRevenue { get; set; }

        /// <summary>آخرین تاریخی که درآمد برای آن شناسایی شده است</summary>
        [Display(Name = "آخرین شناسایی درآمد")]
        [DataType(DataType.Date)]
        public DateTime? LastRevenueRecognitionDate { get; set; }

        /// <summary>وضعیت پرداخت (محاسبه خودکار)</summary>
        [Required]
        [StringLength(20)]
        [Display(Name = "وضعیت پرداخت")]
        public string PaymentStatus { get; set; } = "Unpaid"; // Unpaid, PartiallyPaid, FullyPaid

        // ── Dates ──
        [Required(ErrorMessage = "تاریخ شروع الزامی است.")]
        [DataType(DataType.Date)]
        [Display(Name = "تاریخ شروع")]
        public DateTime StartDate { get; set; } = DateTime.Today;

        [DataType(DataType.Date)]
        [Display(Name = "تاریخ پایان")]
        public DateTime? EndDate { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "تاریخ امضا")]
        public DateTime? SignDate { get; set; }

        /// <summary>تعداد روزهای قبل از انقضا برای اخطار (پیش‌فرض: 30)</summary>
        [Display(Name = "روزهای اخطار انقضا")]
        public int ExpiryNoticeDays { get; set; } = 30;

        // ── Auto-Renewal ─────────────────────────────────────────────────
        /// <summary>آیا قرارداد به صورت خودکار تمدید می‌شود</summary>
        [Display(Name = "تمدید خودکار")]
        public bool IsAutoRenew { get; set; }

        /// <summary>تعداد روز قبل از انقضا برای اجرای تمدید خودکار (پیش‌فرض: 7)</summary>
        [Display(Name = "تمدید خودکار N روز قبل از انقضا")]
        public int AutoRenewDaysBeforeExpiry { get; set; } = 7;

        /// <summary>مدت تمدید به ماه (پیش‌فرض: 12)</summary>
        [Display(Name = "مدت تمدید (ماه)")]
        public int AutoRenewTermMonths { get; set; } = 12;

        /// <summary>تاریخ لغو تمدید خودکار (اگر کاربر لغو کرده باشد)</summary>
        [Display(Name = "تاریخ لغو تمدید خودکار")]
        public DateTime? AutoRenewalCancelledAt { get; set; }

        /// <summary>کاربری که تمدید خودکار را لغو کرده است</summary>
        [Display(Name = "لغوکننده تمدید خودکار")]
        [StringLength(450)]
        public string? AutoRenewalCancelledByUserId { get; set; }

        /// <summary>کاربر لغوکننده تمدید خودکار</summary>
        [Display(Name = "لغوکننده تمدید خودکار")]
        public User? AutoRenewalCancelledByUser { get; set; }

        /// <summary>آخرین تاریخ تمدید خودکار</summary>
        [Display(Name = "آخرین تمدید خودکار")]
        public DateTime? LastAutoRenewedAt { get; set; }

        // ── Status ──
        [Required]
        [StringLength(40)]
        [Display(Name = "وضعیت")]
        public string Status { get; set; } = ContractStatuses.Draft;

        // ── Responsible / Assignment ──
        /// <summary>کاربر مسئول پیگیری (AssignedUserId)</summary>
        [Display(Name = "مسئول پیگیری")]
        public string? AssignedUserId { get; set; }

        [Display(Name = "مسئول پیگیری")]
        public User? AssignedUser { get; set; }

        [Required]
        public string CreatedByUserId { get; set; } = string.Empty;

        public User? CreatedByUser { get; set; }

        /// <summary>قالب/تمپلیت انتخابی برای این قرارداد</summary>
        [Display(Name = "قالب قرارداد")]
        public int? TemplateId { get; set; }

        [Display(Name = "قالب قرارداد")]
        public ContractTemplate? ContractTemplate { get; set; }

        /// <summary>متن قرارداد با فیلدهای جایگزاری شده</summary>
        [Display(Name = "متن قرارداد")]
        public string? ContractBodyHtml { get; set; }

        // ── Archive ──
        /// <summary>بایگانی شده یا خیر</summary>
        public bool IsArchived { get; set; }

        /// <summary>تاریخ بایگانی</summary>
        [Display(Name = "تاریخ بایگانی")]
        public DateTime? ArchivedAt { get; set; }

        /// <summary>کاربر بایگانی‌کننده</summary>
        [Display(Name = "بایگانی‌کننده")]
        public string? ArchivedByUserId { get; set; }

        [Display(Name = "بایگانی‌کننده")]
        public User? ArchivedByUser { get; set; }

        // ── Timestamps ──
        [Display(Name = "تاریخ ایجاد")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Display(Name = "آخرین ویرایش")]
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        // ── Navigation ──
        public List<ContractPayment> Payments { get; set; } = new();
        public List<ContractAttachment> Attachments { get; set; } = new();
        public List<ContractAuditLog> AuditLogs { get; set; } = new();
        public List<ContractAmendment> Amendments { get; set; } = new();
        public List<ContractTaxLine> TaxLines { get; set; } = new();
        public List<ContractFieldValue> FieldValues { get; set; } = new();
        public List<AutoRenewalLog> AutoRenewalLogs { get; set; } = new();
    }
}