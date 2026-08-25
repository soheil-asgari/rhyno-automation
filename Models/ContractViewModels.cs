using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace OfficeAutomation.Models
{
    /// <summary>
    /// ViewModel صفحه فهرست قراردادها
    /// </summary>
    public class ContractIndexVM
    {
        [Display(Name = "جستجو")]
        public string? SearchTerm { get; set; }

        [Display(Name = "وضعیت")]
        public string? Status { get; set; }

        [Display(Name = "نوع قرارداد")]
        public string? ContractType { get; set; }

        [Display(Name = "مسئول پیگیری")]
        public string? AssignedUserId { get; set; }

        [Display(Name = "طرف قرارداد")]
        public string? ClientName { get; set; }

        public int? CustomerId { get; set; }

        [Display(Name = "وضعیت پرداخت")]
        public string? PaymentStatus { get; set; }

        [Display(Name = "از تاریخ شروع")]
        [DataType(DataType.Date)]
        public DateTime? StartDateFrom { get; set; }

        [Display(Name = "تا تاریخ شروع")]
        [DataType(DataType.Date)]
        public DateTime? StartDateTo { get; set; }

        [Display(Name = "از تاریخ پایان")]
        [DataType(DataType.Date)]
        public DateTime? EndDateFrom { get; set; }

        [Display(Name = "تا تاریخ پایان")]
        [DataType(DataType.Date)]
        public DateTime? EndDateTo { get; set; }

        [Display(Name = "حداقل مبلغ")]
        public decimal? MinAmount { get; set; }

        [Display(Name = "حداکثر مبلغ")]
        public decimal? MaxAmount { get; set; }

        [Display(Name = "فقط در شرف انقضا")]
        public bool? ExpiringSoonOnly { get; set; }

        [Display(Name = "نمایش بایگانی")]
        public bool ShowArchived { get; set; }

        // ── Sorting & Pagination ──
        public string? SortBy { get; set; }
        public bool? SortDescending { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public int TotalPages { get; set; }

        // ── Summary Stats ──
        public int TotalCount { get; set; }
        public int ActiveCount { get; set; }
        public int CompletedCount { get; set; }
        public int DraftCount { get; set; }
        public int ExpiringSoonCount { get; set; }
        public int FilteredCount { get; set; }
        public decimal TotalAmountSum { get; set; }
        public decimal TotalPaidSum { get; set; }

        // ── Dropdown Options ──
        public List<SelectListItem> StatusOptions { get; set; } = new();
        public List<SelectListItem> ContractTypeOptions { get; set; } = new();
        public List<SelectListItem> ResponsibleUserOptions { get; set; } = new();
        public List<SelectListItem> CustomerOptions { get; set; } = new();
        public List<SelectListItem> PaymentStatusOptions { get; set; } = new();
        public List<ContractIndexItemVM> Items { get; set; } = new();

        // ── Column Customization ──
        /// <summary>JSON array of selected column names. Null means all columns shown.</summary>
        public string? SavedColumnsJson { get; set; }
    }

    public class ContractIndexItemVM
    {
        public int Id { get; set; }
        public string ContractNumber { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string ClientName { get; set; } = string.Empty;
        public string? CustomerName { get; set; }
        public string? ContractType { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal RemainingBalance { get; set; }
        public string? PaymentStatus { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? ResponsibleUserName { get; set; }
        public string? AssignedUserId { get; set; }
        public bool IsArchived { get; set; }
        public bool IsOwnedByCurrentUser { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ArchivedAt { get; set; }
        public string? ArchivedByUserName { get; set; }
        /// <summary>Payments for dynamic row-level calculation in Index</summary>
        public List<ContractPayment> Payments { get; set; } = new();
    }

    /// <summary>
    /// ViewModel ایجاد قرارداد جدید
    /// </summary>
    public class ContractCreateVM
    {
        [Display(Name = "شماره قرارداد")]
        public string? ContractNumber { get; set; } // Auto-generated if empty

        [Required(ErrorMessage = "عنوان قرارداد الزامی است.")]
        [StringLength(200, ErrorMessage = "عنوان قرارداد نمی‌تواند بیشتر از 200 کاراکتر باشد.")]
        [Display(Name = "عنوان قرارداد")]
        public string Title { get; set; } = string.Empty;

        [Display(Name = "نوع قرارداد")]
        public string? ContractType { get; set; }

        [Display(Name = "مشتری")]
        public int? CustomerId { get; set; }

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

        [Display(Name = "یادداشت‌ها")]
        public string? Notes { get; set; }

        [Display(Name = "شرایط و ضوابط")]
        public string? TermsAndConditions { get; set; }

        [Display(Name = "متن قرارداد")]
        public string? ContractBodyHtml { get; set; }

        [Required(ErrorMessage = "مبلغ کل قرارداد الزامی است.")]
        [Range(0, 999999999999, ErrorMessage = "مبلغ کل معتبر نیست.")]
        [Display(Name = "مبلغ کل")]
        public decimal TotalAmount { get; set; }

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

        // ── Jalali display fields (string) ──
        // Keep the real DateTime bound to a hidden ISO input so model binding
        // always succeeds. These text inputs display the Jalali (shamsi) date
        // to the user and are converted to ISO on the client before submit.
        [Display(Name = "تاریخ شروع")]
        public string? StartDateDisplay { get; set; }

        [Display(Name = "تاریخ پایان")]
        public string? EndDateDisplay { get; set; }

        [Display(Name = "تاریخ امضا")]
        public string? SignDateDisplay { get; set; }

        [Display(Name = "روزهای اخطار انقضا")]
        public int ExpiryNoticeDays { get; set; } = 30;

        // ── Auto-Renewal ───────────────────────────────────────────
        [Display(Name = "تمدید خودکار")]
        public bool IsAutoRenew { get; set; }

        [Display(Name = "تمدید خودکار N روز قبل از انقضا")]
        public int AutoRenewDaysBeforeExpiry { get; set; } = 7;

        [Display(Name = "مدت تمدید (ماه)")]
        public int AutoRenewTermMonths { get; set; } = 12;

        [Display(Name = "وضعیت")]
        public string Status { get; set; } = ContractStatuses.Draft;

        [Display(Name = "مسئول پیگیری")]
        public string? AssignedUserId { get; set; }

        [Display(Name = "قالب قرارداد")]
        public int? TemplateId { get; set; }

        public List<SelectListItem> StatusOptions { get; set; } = new();
        public List<SelectListItem> ContractTypeOptions { get; set; } = new();
        public List<SelectListItem> UserOptions { get; set; } = new();
        public List<SelectListItem> TemplateOptions { get; set; } = new();
        public List<SelectListItem> CustomerOptions { get; set; } = new();
public string? WorkflowId { get; set; }
        public List<ContractTypeFieldVM> TypeFields { get; set; } = new();
        public Dictionary<string, string> FieldValues { get; set; } = new();
    }

    /// <summary>
    /// ViewModel ویرایش قرارداد
    /// </summary>
    public class ContractEditVM
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

        [Display(Name = "نوع قرارداد")]
        public string? ContractType { get; set; }

        [Display(Name = "مشتری")]
        public int? CustomerId { get; set; }

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

        [Display(Name = "یادداشت‌ها")]
        public string? Notes { get; set; }

        [Display(Name = "شرایط و ضوابط")]
        public string? TermsAndConditions { get; set; }

        [Display(Name = "متن قرارداد")]
        public string? ContractBodyHtml { get; set; }

        [Required(ErrorMessage = "مبلغ کل قرارداد الزامی است.")]
        [Range(0, 999999999999, ErrorMessage = "مبلغ کل معتبر نیست.")]
        [Display(Name = "مبلغ کل")]
        public decimal TotalAmount { get; set; }

        [Display(Name = "مبلغ پرداخت شده")]
        public decimal PaidAmount { get; set; }

        [Display(Name = "باقی مانده")]
        public decimal RemainingBalance { get; set; }

        [Display(Name = "وضعیت پرداخت")]
        public string? PaymentStatus { get; set; }

        [Required(ErrorMessage = "تاریخ شروع الزامی است.")]
        [DataType(DataType.Date)]
        [Display(Name = "تاریخ شروع")]
        public DateTime StartDate { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "تاریخ پایان")]
        public DateTime? EndDate { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "تاریخ امضا")]
        public DateTime? SignDate { get; set; }

        // ── Jalali display fields (string) ──
        [Display(Name = "تاریخ شروع")]
        public string? StartDateDisplay { get; set; }

        [Display(Name = "تاریخ پایان")]
        public string? EndDateDisplay { get; set; }

        [Display(Name = "تاریخ امضا")]
        public string? SignDateDisplay { get; set; }

        [Display(Name = "روزهای اخطار انقضا")]
        public int ExpiryNoticeDays { get; set; } = 30;

        // ── Auto-Renewal ───────────────────────────────────────────
        [Display(Name = "تمدید خودکار")]
        public bool IsAutoRenew { get; set; }

        [Display(Name = "تمدید خودکار N روز قبل از انقضا")]
        public int AutoRenewDaysBeforeExpiry { get; set; } = 7;

        [Display(Name = "مدت تمدید (ماه)")]
        public int AutoRenewTermMonths { get; set; } = 12;

        [Required(ErrorMessage = "وضعیت الزامی است.")]
        [Display(Name = "وضعیت")]
        public string Status { get; set; } = ContractStatuses.Draft;

        [Display(Name = "مسئول پیگیری")]
        public string? AssignedUserId { get; set; }

        [Display(Name = "قالب قرارداد")]
        public int? TemplateId { get; set; }

        public List<SelectListItem> StatusOptions { get; set; } = new();
        public List<SelectListItem> ContractTypeOptions { get; set; } = new();
        public List<SelectListItem> UserOptions { get; set; } = new();
        public List<SelectListItem> TemplateOptions { get; set; } = new();
public List<ContractTypeFieldVM> TypeFields { get; set; } = new();
        public Dictionary<string, string> FieldValues { get; set; } = new();
        public List<SelectListItem> CustomerOptions { get; set; } = new();
    }

    /// <summary>
    /// ViewModel جزئیات قرارداد
    /// </summary>
    public class ContractDetailsVM
    {
        public int Id { get; set; }
        public string ContractNumber { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? ContractType { get; set; }
        public string ClientName { get; set; } = string.Empty;
        public string? ClientPhone { get; set; }
        public string? ClientAddress { get; set; }
        public int? CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public string? Description { get; set; }
        public string? Notes { get; set; }
        public string? TermsAndConditions { get; set; }
        public string? ContractBodyHtml { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal RemainingBalance { get; set; }
        public string? PaymentStatus { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public DateTime? SignDate { get; set; }
        public int ExpiryNoticeDays { get; set; } = 30;
        public string Status { get; set; } = string.Empty;
        public string? ResponsibleUserName { get; set; }
        public string? AssignedUserId { get; set; }
        public string? TemplateName { get; set; }
        public bool IsArchived { get; set; }
        public DateTime? ArchivedAt { get; set; }
        public string? ArchivedByUserName { get; set; }
        public string CreatedByUserName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public bool CanEdit { get; set; }
        public bool CanViewFinancials { get; set; }
        public int DaysRemaining { get; set; }

        // ── Auto-Renewal ───────────────────────────────────────────
        public bool IsAutoRenew { get; set; }
        public int AutoRenewDaysBeforeExpiry { get; set; } = 7;
        public int AutoRenewTermMonths { get; set; } = 12;
        public DateTime? AutoRenewalCancelledAt { get; set; }
        public string? AutoRenewalCancelledByUserName { get; set; }
        public DateTime? LastAutoRenewedAt { get; set; }
        public List<AutoRenewalLogVM> AutoRenewalLogs { get; set; } = new();

        public List<ContractPaymentVM> Payments { get; set; } = new();
        public List<ContractAttachmentVM> Attachments { get; set; } = new();
public List<LifecycleStepVM> LifecycleSteps { get; set; } = new();
        public List<ContractAuditLogVM> AuditLogs { get; set; } = new();
        public List<AmendmentListItemVM> Amendments { get; set; } = new();
public List<ContractTypeFieldVM> TypeFields { get; set; } = new();
        public Dictionary<string, string> FieldValues { get; set; } = new();
    }

    public class ContractPaymentVM
    {
        public int Id { get; set; }
        public decimal Amount { get; set; }
        public DateTime PaymentDate { get; set; }
        public DateTime? DueDate { get; set; }
        public string? Status { get; set; }
        public string? PaymentMethod { get; set; }
        public string? ReferenceNumber { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class ContractAttachmentVM
    {
        public int Id { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string? ContentType { get; set; }
        public long FileSize { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? UploadedByUserName { get; set; }
    }

    public class ContractAuditLogVM
    {
        public int Id { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string? OldStatus { get; set; }
        public string? NewStatus { get; set; }
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// ViewModel تنظیمات قرارداد
    /// </summary>
    public class ContractSettingsVM
    {
        public string UserId { get; set; } = string.Empty;

        [Display(Name = "نام قالب")]
        public string TemplateName { get; set; } = string.Empty;

        [StringLength(500)]
        [Display(Name = "توضیحات")]
        public string? Description { get; set; }

        [Display(Name = "قالب HTML")]
        public string? HtmlContent { get; set; }

        [Display(Name = "قالب پیش‌فرض")]
        public bool IsDefault { get; set; }

        public int? CurrentTemplateId { get; set; }

        [Display(Name = "وضعیت پیش‌فرض")]
        public string DefaultStatus { get; set; } = ContractStatuses.Draft;

        [Display(Name = "مسئول پیگیری پیش‌فرض")]
        public string? DefaultResponsibleUserId { get; set; }

        [Display(Name = "اعلان هنگام ایجاد قرارداد")]
        public bool NotifyOnCreate { get; set; }

        public List<SelectListItem> StatusOptions { get; set; } = new();
        public List<SelectListItem> UserOptions { get; set; } = new();
        public List<ContractTemplateListItemVM> Templates { get; set; } = new();

        /// <summary>
        /// تگ‌های قابل استفاده در ویرایشگر قالب (برای نمایش در سایدبار)
        /// </summary>
        public List<ContractTemplateTagVM> AvailableTags { get; set; } = new();
    }

    public class ContractTemplateTagVM
    {
        public string Tag { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    public class ContractTemplateListItemVM
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsDefault { get; set; }
        public string? TemplateType { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    /// <summary>
    /// ViewModel ثبت پرداخت جدید
    /// </summary>
    public class ContractPaymentCreateVM
    {
        [Required]
        public int ContractId { get; set; }

        [Required(ErrorMessage = "مبلغ پرداخت الزامی است.")]
        [Range(0, 999999999999, ErrorMessage = "مبلغ پرداخت معتبر نیست.")]
        [Display(Name = "مبلغ پرداخت")]
        public decimal Amount { get; set; }

        [Required(ErrorMessage = "تاریخ پرداخت الزامی است.")]
        [DataType(DataType.Date)]
        [Display(Name = "تاریخ پرداخت")]
        public DateTime PaymentDate { get; set; } = DateTime.Today;

        [DataType(DataType.Date)]
        [Display(Name = "تاریخ سررسید")]
        public DateTime? DueDate { get; set; }

        [Display(Name = "وضعیت")]
        public string Status { get; set; } = "Paid";

        [StringLength(60)]
        [Display(Name = "روش پرداخت")]
        public string? PaymentMethod { get; set; }

        [StringLength(120)]
        [Display(Name = "شماره مرجع")]
        public string? ReferenceNumber { get; set; }

        [StringLength(500)]
        [Display(Name = "توضیحات")]
        public string? Notes { get; set; }
    }

    /// <summary>
    /// ViewModel فهرست بایگانی
    /// </summary>
    public class ContractArchiveVM
    {
        [Display(Name = "جستجو")]
        public string? SearchTerm { get; set; }

        [Display(Name = "وضعیت")]
        public string? Status { get; set; }

        [Display(Name = "نوع قرارداد")]
        public string? ContractType { get; set; }

        [Display(Name = "وضعیت پرداخت")]
        public string? PaymentStatus { get; set; }

        [Display(Name = "مسئول")]
        public string? AssignedUserId { get; set; }

        [Display(Name = "از تاریخ شروع")]
        [DataType(DataType.Date)]
        public DateTime? StartDateFrom { get; set; }

        [Display(Name = "تا تاریخ شروع")]
        [DataType(DataType.Date)]
        public DateTime? StartDateTo { get; set; }

        public List<SelectListItem> StatusOptions { get; set; } = new();
        public List<SelectListItem> ContractTypeOptions { get; set; } = new();
        public List<SelectListItem> PaymentStatusOptions { get; set; } = new();
        public List<SelectListItem> ResponsibleUserOptions { get; set; } = new();
        public List<ContractIndexItemVM> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int FilteredCount { get; set; }
    }

    /// <summary>
    /// ViewModel داشبورد قراردادها
    /// </summary>
    public class ContractDashboardVM
    {
        public int TotalContracts { get; set; }
        public int ActiveContracts { get; set; }
        public int ExpiredContracts { get; set; }
        public int ArchivedContracts { get; set; }
        public int DraftContracts { get; set; }
        public int ExpiringSoonCount { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal TotalPaid { get; set; }
        public decimal TotalOutstanding { get; set; }
        public int FullyPaid { get; set; }
        public int PartiallyPaid { get; set; }
        public int Unpaid { get; set; }
public List<ChartDataPoint> StatusDistribution { get; set; } = new();
        public List<ChartDataPoint> MonthlyValueTrend { get; set; } = new();
        public List<ChartDataPoint> ContractTypeDistribution { get; set; } = new();
    }
/// <summary>Request body for internal signature capture</summary>
public class SignContractRequest
{
    /// <summary>تصویر امضا به صورت Base64</summary>
    [Required(ErrorMessage = "داده‌های امضا الزامی است.")]
    public string SignatureImageData { get; set; } = string.Empty;

    /// <summary>توضیح اختیاری</summary>
    public string? Comment { get; set; }
}

/// <summary>Request body for generating an external sign link</summary>
public class GetExternalSignLinkRequest
{
    /// <summary>شناسه امضاکننده</summary>
    [Required]
    public int SignerId { get; set; }
}

/// <summary>Request body for creating a signature request</summary>
public class CreateSignatureRequestVm
{
    /// <summary>عنوان درخواست امضا</summary>
    [Required(ErrorMessage = "عنوان الزامی است.")]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    /// <summary>پیام به امضاکنندگان</summary>
    [StringLength(2000)]
    public string? Message { get; set; }

    /// <summary>روش ارسال: InApp, Email, SMS</summary>
    [Required]
    public string DeliveryMethod { get; set; } = SignatureDeliveryMethod.InApp;

    /// <summary>ترتیب امضا: Sequential, Parallel</summary>
    [Required]
    public string SigningOrder { get; set; } = "Sequential";

    /// <summary>فعال‌سازی خودکار قرارداد پس از تکمیل امضاها</summary>
    public bool AutoActivateContract { get; set; }

    /// <summary>اصلاحیه مرتبط (اختیاری)</summary>
    public int? AmendmentId { get; set; }
}
public class AmendmentListItemVM
{
    public int Id { get; set; }
    public string AmendmentType { get; set; } = string.Empty;
    public string AmendmentTypeDisplay { get; set; } = string.Empty;
    public int AmendmentNumber { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal AmountChange { get; set; }
    public decimal? NewTotalAmount { get; set; }
    public int? EndDateChangeDays { get; set; }
    public DateTime? NewEndDate { get; set; }
    public DateTime EffectiveDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool CanEdit { get; set; }
    public bool CanApply { get; set; }
    public string CreatedByUserName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string CreatedAtText { get; set; } = string.Empty;
}

public class AmendmentDetailVM
{
    public int Id { get; set; }
    public int ContractId { get; set; }
    public string AmendmentType { get; set; } = string.Empty;
    public int AmendmentNumber { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal AmountChange { get; set; }
    public decimal? NewTotalAmount { get; set; }
    public int? EndDateChangeDays { get; set; }
    public DateTime? NewEndDate { get; set; }
    public DateTime EffectiveDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public string CreatedByUserName { get; set; } = string.Empty;
    public string? AppliedByUserName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? AppliedAt { get; set; }
    public int? ApprovalRequestId { get; set; }
}

public class AmendmentCreateVM
{
    [Required] public int ContractId { get; set; }
    [Required][StringLength(20)] public string AmendmentType { get; set; } = AmendmentTypeNames.Amendment;
    [Required][StringLength(200)] public string Title { get; set; } = string.Empty;
    [StringLength(4000)] public string? Description { get; set; }
    public decimal AmountChange { get; set; }
    public int? EndDateChangeDays { get; set; }
    [DataType(DataType.Date)] public DateTime? NewEndDate { get; set; }
    [Required][DataType(DataType.Date)] public DateTime EffectiveDate { get; set; } = DateTime.Today;
    public bool RequiresApproval { get; set; } = true;
    public string? ApprovalWorkflowConfigJson { get; set; }
}

public class AmendmentEditVM
{
    [Required][StringLength(200)] public string Title { get; set; } = string.Empty;
    [StringLength(4000)] public string? Description { get; set; }
    public decimal AmountChange { get; set; }
    public int? EndDateChangeDays { get; set; }
    [DataType(DataType.Date)] public DateTime? NewEndDate { get; set; }
    [Required][DataType(DataType.Date)] public DateTime EffectiveDate { get; set; } = DateTime.Today;
}

public class LifecycleStepVM
{
    public string Label { get; set; } = string.Empty;
    public string StatusKey { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public DateTime? Date { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? UserName { get; set; }
    public string? CompletedByUserName { get; set; }
    public bool IsCompleted { get; set; }
    public bool IsActive { get; set; }
    public bool IsCurrent { get; set; }
    public string? Color { get; set; }
    public string? Description { get; set; }
    public string? RejectionReason { get; set; }
    public int? RejectionCount { get; set; }
    public string? ApprovalDecision { get; set; }
}

public class AutoRenewalLogVM
{
    public int Id { get; set; }
    public int ContractId { get; set; }
    public int? RenewedContractId { get; set; }
    public DateTime RenewedAt { get; set; }
    public string RenewedAtText { get; set; } = string.Empty;
    public DateTime? PreviousEndDate { get; set; }
    public string? PreviousEndDateText { get; set; }
    public DateTime? NewEndDate { get; set; }
    public string? NewEndDateText { get; set; }
    public int RenewalTermMonths { get; set; }
    public string Status { get; set; } = "Success";
    public string? Notes { get; set; }
    public string? CancelledByUserName { get; set; }
}

/// <summary>
/// Request model for saving column visibility preferences.
/// </summary>
public class SaveColumnPreferenceRequest
{
    /// <summary>نام ستون‌های انتخاب‌شده</summary>
    public string[] ColumnNames { get; set; } = Array.Empty<string>();
}
}
