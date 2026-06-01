using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace OfficeAutomation.Models
{
    public static class HumanCapitalProcessTypes
    {
        public const string Recruitment = "استخدام";
        public const string Termination = "تعدیل";
        public const string Resignation = "ترک کار";
        public const string EndOfService = "پایان خدمت";

        public static readonly IReadOnlyList<string> All = new[]
        {
            Recruitment,
            Termination,
            Resignation,
            EndOfService
        };
    }

    public class HumanCapitalIndexVM
    {
        [Display(Name = "جستجو")]
        public string? SearchTerm { get; set; }

        [Display(Name = "وضعیت")]
        public string? Status { get; set; }

        [Display(Name = "دپارتمان")]
        public int? DepartmentId { get; set; }

        public int TotalCount { get; set; }

        public int FilteredCount { get; set; }

        public int ActiveCount { get; set; }

        public int SeparatedCount { get; set; }

        public List<SelectListItem> DepartmentOptions { get; set; } = new();

        public List<string> StatusOptions { get; set; } = new();

        public List<HumanCapitalIndexItemVM> Items { get; set; } = new();
    }

    public class HumanCapitalIndexItemVM
    {
        public int Id { get; set; }

        public string PersonnelCode { get; set; } = string.Empty;

        public string FullName { get; set; } = string.Empty;

        public string? DepartmentName { get; set; }

        public string PositionTitle { get; set; } = string.Empty;

        public DateTime HireDate { get; set; }

        public decimal CurrentSalary { get; set; }

        public string CurrentStatus { get; set; } = string.Empty;

        public int SalaryChangeCount { get; set; }

        public DateTime? LatestStatusDate { get; set; }
    }

    public class HumanCapitalBaseUpsertVM
    {
        [Required(ErrorMessage = "کد پرسنلی الزامی است.")]
        [StringLength(30, ErrorMessage = "کد پرسنلی نمی‌تواند بیشتر از 30 کاراکتر باشد.")]
        [Display(Name = "کد پرسنلی")]
        public string PersonnelCode { get; set; } = string.Empty;

        [Required(ErrorMessage = "نام و نام خانوادگی الزامی است.")]
        [StringLength(120, ErrorMessage = "نام نمی‌تواند بیشتر از 120 کاراکتر باشد.")]
        [Display(Name = "نام و نام خانوادگی")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "کد ملی الزامی است.")]
        [StringLength(20, ErrorMessage = "کد ملی نمی‌تواند بیشتر از 20 کاراکتر باشد.")]
        [Display(Name = "کد ملی")]
        public string NationalCode { get; set; } = string.Empty;

        [DataType(DataType.Date)]
        [Display(Name = "تاریخ تولد")]
        public DateTime BirthDate { get; set; } = DateTime.Today.AddYears(-25);

        [DataType(DataType.Date)]
        [Display(Name = "تاریخ استخدام")]
        public DateTime HireDate { get; set; } = DateTime.Today;

        [DataType(DataType.Date)]
        [Display(Name = "تاریخ پایان قرارداد")]
        public DateTime? ContractEndDate { get; set; }

        [Display(Name = "تکمیل ورود به کار")]
        public bool OnboardingCompleted { get; set; } = true;

        [Display(Name = "دپارتمان")]
        public int? DepartmentId { get; set; }

        [Required(ErrorMessage = "عنوان شغلی الزامی است.")]
        [StringLength(100, ErrorMessage = "عنوان شغلی نمی‌تواند بیشتر از 100 کاراکتر باشد.")]
        [Display(Name = "عنوان شغلی")]
        public string PositionTitle { get; set; } = string.Empty;

        [Required(ErrorMessage = "نوع همکاری الزامی است.")]
        [StringLength(60, ErrorMessage = "نوع همکاری نمی‌تواند بیشتر از 60 کاراکتر باشد.")]
        [Display(Name = "نوع همکاری")]
        public string EmploymentType { get; set; } = string.Empty;

        [Range(0, 99999999999, ErrorMessage = "مبلغ حقوق معتبر نیست.")]
        [Display(Name = "حقوق پایه")]
        public decimal CurrentSalary { get; set; }

        [StringLength(20, ErrorMessage = "شماره تماس نمی‌تواند بیشتر از 20 کاراکتر باشد.")]
        [Display(Name = "شماره تماس")]
        public string? PhoneNumber { get; set; }

        [StringLength(120, ErrorMessage = "ایمیل نمی‌تواند بیشتر از 120 کاراکتر باشد.")]
        [EmailAddress(ErrorMessage = "فرمت ایمیل معتبر نیست.")]
        [Display(Name = "ایمیل")]
        public string? Email { get; set; }

        [StringLength(300, ErrorMessage = "آدرس نمی‌تواند بیشتر از 300 کاراکتر باشد.")]
        [Display(Name = "آدرس")]
        public string? Address { get; set; }

        [StringLength(1000, ErrorMessage = "توضیحات نمی‌تواند بیشتر از 1000 کاراکتر باشد.")]
        [Display(Name = "توضیحات پرونده")]
        public string? Notes { get; set; }

        public List<SelectListItem> DepartmentOptions { get; set; } = new();

        public List<SelectListItem> EmploymentTypeOptions { get; set; } = new();
    }

    public class HumanCapitalCreateVM : HumanCapitalBaseUpsertVM
    {

        [Required(ErrorMessage = "تاریخ رخداد استخدام الزامی است.")]
        [DataType(DataType.Date)]
        [Display(Name = "تاریخ رخداد")]
        public DateTime InitialStatusDate { get; set; } = DateTime.Today;

        [StringLength(120, ErrorMessage = "شماره مرجع نمی‌تواند بیشتر از 120 کاراکتر باشد.")]
        [Display(Name = "شماره مرجع")]
        public string? InitialReferenceNumber { get; set; }

        [Required(ErrorMessage = "توضیح رخداد الزامی است.")]
        [StringLength(500, ErrorMessage = "توضیحات رخداد نمی‌تواند بیشتر از 500 کاراکتر باشد.")]
        [Display(Name = "توضیح رخداد")]
        public string InitialStatusDescription { get; set; } = "استخدام و تشکیل پرونده پرسنلی";
    }

    public class HumanCapitalEditVM : HumanCapitalBaseUpsertVM
    {
        public int Id { get; set; }

        [Required]
        [StringLength(40)]
        public string CurrentStatus { get; set; } = "فعال";
    }

    public class HumanCapitalDetailsVM
    {
        public int Id { get; set; }

        public string PersonnelCode { get; set; } = string.Empty;

        public string FullName { get; set; } = string.Empty;

        public string NationalCode { get; set; } = string.Empty;

        public DateTime BirthDate { get; set; }

        public DateTime HireDate { get; set; }

        public DateTime? ContractEndDate { get; set; }

        public bool OnboardingCompleted { get; set; }

        public string? DepartmentName { get; set; }

        public string PositionTitle { get; set; } = string.Empty;

        public string EmploymentType { get; set; } = string.Empty;

        public decimal CurrentSalary { get; set; }

        public string CurrentStatus { get; set; } = string.Empty;

        public string? PhoneNumber { get; set; }

        public string? Email { get; set; }

        public string? Address { get; set; }

        public string? Notes { get; set; }

        public List<HumanCapitalSalaryHistoryVM> SalaryHistories { get; set; } = new();

        public List<HumanCapitalStatusHistoryVM> StatusHistories { get; set; } = new();

        public HumanCapitalSalaryIncreaseVM SalaryIncrease { get; set; } = new();

        public HumanCapitalStatusChangeVM StatusChange { get; set; } = new();

        public List<SelectListItem> ProcessTypeOptions { get; set; } = new();
    }

    public class HumanCapitalSalaryHistoryVM
    {
        public DateTime EffectiveDate { get; set; }

        public decimal PreviousSalary { get; set; }

        public decimal NewSalary { get; set; }

        public string? PromotionTitle { get; set; }

        public string Reason { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }
    }

    public class HumanCapitalStatusHistoryVM
    {
        public string StatusType { get; set; } = string.Empty;

        public DateTime EffectiveDate { get; set; }

        public string? ReferenceNumber { get; set; }

        public string Description { get; set; } = string.Empty;

        public string? ExitReason { get; set; }

        public DateTime CreatedAt { get; set; }
    }

    public class HumanCapitalSalaryIncreaseVM
    {
        public int EmployeeId { get; set; }

        [Required(ErrorMessage = "تاریخ اعمال افزایش حقوق الزامی است.")]
        [DataType(DataType.Date)]
        [Display(Name = "تاریخ اعمال")]
        public DateTime EffectiveDate { get; set; } = DateTime.Today;

        [Range(1, 99999999999, ErrorMessage = "حقوق جدید معتبر نیست.")]
        [Display(Name = "حقوق جدید")]
        public decimal NewSalary { get; set; }

        [StringLength(120, ErrorMessage = "عنوان ارتقا نمی‌تواند بیشتر از 120 کاراکتر باشد.")]
        [Display(Name = "عنوان ارتقا")]
        public string? PromotionTitle { get; set; }

        [Required(ErrorMessage = "دلیل افزایش حقوق الزامی است.")]
        [StringLength(500, ErrorMessage = "دلیل افزایش حقوق نمی‌تواند بیشتر از 500 کاراکتر باشد.")]
        [Display(Name = "دلیل افزایش")]
        public string Reason { get; set; } = string.Empty;
    }

    public class HumanCapitalStatusChangeVM
    {
        public int EmployeeId { get; set; }

        [Required(ErrorMessage = "نوع فرآیند الزامی است.")]
        [Display(Name = "نوع فرآیند")]
        public string StatusType { get; set; } = HumanCapitalProcessTypes.Recruitment;

        [Required(ErrorMessage = "تاریخ رخداد الزامی است.")]
        [DataType(DataType.Date)]
        [Display(Name = "تاریخ رخداد")]
        public DateTime EffectiveDate { get; set; } = DateTime.Today;

        [StringLength(120, ErrorMessage = "شماره مرجع نمی‌تواند بیشتر از 120 کاراکتر باشد.")]
        [Display(Name = "شماره مرجع")]
        public string? ReferenceNumber { get; set; }

        [Required(ErrorMessage = "توضیحات رخداد الزامی است.")]
        [StringLength(500, ErrorMessage = "توضیحات رخداد نمی‌تواند بیشتر از 500 کاراکتر باشد.")]
        [Display(Name = "توضیحات رخداد")]
        public string Description { get; set; } = string.Empty;

        [StringLength(500, ErrorMessage = "دلیل خروج نمی‌تواند بیشتر از 500 کاراکتر باشد.")]
        [Display(Name = "دلیل خروج")]
        public string? ExitReason { get; set; }
    }
}
