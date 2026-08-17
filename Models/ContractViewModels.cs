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

        [Display(Name = "روزهای اخطار انقضا")]
        public int ExpiryNoticeDays { get; set; } = 30;

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

        [Display(Name = "روزهای اخطار انقضا")]
        public int ExpiryNoticeDays { get; set; } = 30;

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

        public List<ContractPaymentVM> Payments { get; set; } = new();
        public List<ContractAttachmentVM> Attachments { get; set; } = new();
        public List<ContractAuditLogVM> AuditLogs { get; set; } = new();
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
    }
}
