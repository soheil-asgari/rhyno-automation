using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace OfficeAutomation.Models
{
    public class SettingsIndexViewModel
    {
        public GeneralSettingsViewModel General { get; set; } = new();
        public ProfileSettingsViewModel Profile { get; set; } = new();
        public ChangePasswordViewModel Password { get; set; } = new();
        public SystemConnectivitySettingsViewModel System { get; set; } = new();
        public UiPreferencesViewModel Ui { get; set; } = new();
        public string? CurrentUserSignaturePath { get; set; }
        public bool CanManageSecurity { get; set; }

        public IReadOnlyCollection<SelectListItem> Languages { get; set; } = Array.Empty<SelectListItem>();
        public IReadOnlyCollection<SelectListItem> TimeZones { get; set; } = Array.Empty<SelectListItem>();
        public IReadOnlyCollection<SelectListItem> Themes { get; set; } = Array.Empty<SelectListItem>();
    }

    public class GeneralSettingsViewModel
    {
        [Required(ErrorMessage = "عنوان/برند سامانه را وارد کنید.")]
        [StringLength(120, ErrorMessage = "عنوان سامانه نباید بیشتر از 120 کاراکتر باشد.")]
        [Display(Name = "عنوان سامانه")]
        public string ApplicationTitle { get; set; } = string.Empty;

        [Required(ErrorMessage = "انتخاب زبان سامانه الزامی است.")]
        [StringLength(16)]
        [Display(Name = "زبان سامانه")]
        public string SystemLanguage { get; set; } = "fa-IR";

        [Required(ErrorMessage = "انتخاب منطقه زمانی الزامی است.")]
        [StringLength(120)]
        [Display(Name = "منطقه زمانی")]
        public string TimeZoneId { get; set; } = "Asia/Tehran";
    }

    public class ProfileSettingsViewModel
    {
        [Required(ErrorMessage = "نام کامل الزامی است.")]
        [StringLength(80, ErrorMessage = "نام کامل نباید بیشتر از 80 کاراکتر باشد.")]
        [Display(Name = "نام کامل")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "ایمیل الزامی است.")]
        [EmailAddress(ErrorMessage = "فرمت ایمیل معتبر نیست.")]
        [Display(Name = "ایمیل")]
        public string Email { get; set; } = string.Empty;

        [Display(Name = "سمت سازمانی")]
        [StringLength(100, ErrorMessage = "سمت سازمانی نباید بیشتر از 100 کاراکتر باشد.")]
        public string? JobTitle { get; set; }

        [Display(Name = "شماره تماس")]
        [Phone(ErrorMessage = "شماره تماس معتبر نیست.")]
        [StringLength(20, ErrorMessage = "شماره تماس نباید بیشتر از 20 کاراکتر باشد.")]
        public string? PhoneNumber { get; set; }

        [Display(Name = "محل خدمت")]
        [StringLength(120, ErrorMessage = "محل خدمت نباید بیشتر از 120 کاراکتر باشد.")]
        public string? ServiceLocation { get; set; }
    }

    public class ChangePasswordViewModel
    {
        [Required(ErrorMessage = "رمز عبور فعلی را وارد کنید.")]
        [DataType(DataType.Password)]
        [Display(Name = "رمز عبور فعلی")]
        public string CurrentPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "رمز عبور جدید را وارد کنید.")]
        [DataType(DataType.Password)]
        [MinLength(6, ErrorMessage = "رمز عبور جدید باید حداقل 6 کاراکتر باشد.")]
        [Display(Name = "رمز عبور جدید")]
        public string NewPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "تکرار رمز عبور جدید الزامی است.")]
        [DataType(DataType.Password)]
        [Compare(nameof(NewPassword), ErrorMessage = "تکرار رمز عبور با رمز جدید مطابقت ندارد.")]
        [Display(Name = "تکرار رمز عبور")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public class SystemConnectivitySettingsViewModel
    {
        public bool IsDatabaseConnected { get; set; }

        [Display(Name = "محیط فعال")]
        public string ActiveEnvironment { get; set; } = "Unknown";

        [Display(Name = "حالت نگهداری")]
        public bool MaintenanceMode { get; set; }

        [Display(Name = "آخرین بروزرسانی")]
        public DateTime? LastUpdatedUtc { get; set; }
    }

    public class UiPreferencesViewModel
    {
        [Display(Name = "سایدبار به‌صورت جمع‌شده باز شود")]
        public bool SidebarCollapsedByDefault { get; set; }

        [Required(ErrorMessage = "انتخاب تم الزامی است.")]
        [RegularExpression("^(Light|Dark|System)$", ErrorMessage = "تم انتخاب شده معتبر نیست.")]
        [Display(Name = "انتخاب تم")]
        public string ThemePreference { get; set; } = "System";
    }

    public class InsuranceEmployeeRowViewModel
    {
        public int? HumanCapitalEmployeeId { get; set; }

        [Required(ErrorMessage = "نام کارمند الزامی است.")]
        [StringLength(100, ErrorMessage = "نام کارمند نباید بیشتر از 100 کاراکتر باشد.")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "عنوان شغل الزامی است.")]
        [StringLength(100, ErrorMessage = "عنوان شغل نباید بیشتر از 100 کاراکتر باشد.")]
        public string JobTitle { get; set; } = string.Empty;

        [Required(ErrorMessage = "تاریخ شروع کار الزامی است.")]
        [Display(Name = "شروع کار (شمسی)")]
        public string StartWorkSolar { get; set; } = string.Empty;

        [Display(Name = "ترک کار (شمسی)")]
        public string? EndWorkSolar { get; set; }

        [Range(0, 31, ErrorMessage = "روز کارکرد باید بین 0 تا 31 باشد.")]
        public int WorkDays { get; set; }

        [Range(0, 99999999999, ErrorMessage = "مبلغ حقوق معتبر نیست.")]
        public decimal Salary { get; set; }

        public bool IsLockedFromHr { get; set; }
    }

    public class InsuranceSaveRequestViewModel
    {
        public int? Id { get; set; }

        [Required(ErrorMessage = "نام پروژه الزامی است.")]
        [StringLength(150, ErrorMessage = "نام پروژه نمی‌تواند بیشتر از 150 کاراکتر باشد.")]
        public string ProjectName { get; set; } = string.Empty;

        [Required(ErrorMessage = "نام مدیر پروژه الزامی است.")]
        [StringLength(100, ErrorMessage = "نام مدیر نمی‌تواند بیشتر از 100 کاراکتر باشد.")]
        public string ManagerName { get; set; } = string.Empty;

        [Range(1, 12, ErrorMessage = "ماه باید بین 1 تا 12 باشد.")]
        public int Month { get; set; }

        [Range(1300, 1600, ErrorMessage = "سال وارد شده معتبر نیست.")]
        public int Year { get; set; }

        [Required(ErrorMessage = "وضعیت لیست بیمه را مشخص کنید.")]
        [StringLength(50)]
        public string Status { get; set; } = "Draft";

        public List<InsuranceEmployeeRowViewModel> Employees { get; set; } = new();
    }

    public class PayrollListPageViewModel
    {
        public int? Id { get; set; }
        public int Month { get; set; }
        public int Year { get; set; }
        public bool IsFinalized { get; set; }
        public string Status { get; set; } = "Draft";
        public List<PayrollEmployeeRowViewModel> Items { get; set; } = new();
        public decimal PreviousMonthTotalNetPayable { get; set; }
        public decimal CurrentMonthTotalNetPayable { get; set; }
        public decimal NetPayableDelta { get; set; }
        public int MissingHrLockCount { get; set; }
        public int DataQualityWarnings { get; set; }
        public List<PayrollQualityWarningVM> Warnings { get; set; } = new();
    }

    public class PayrollHistoryIndexViewModel
    {
        public List<PayrollHistoryRowViewModel> Items { get; set; } = new();
    }

    public class PayrollHistoryRowViewModel
    {
        public int Id { get; set; }
        public int Year { get; set; }
        public int Month { get; set; }
        public decimal TotalBaseSalary { get; set; }
        public decimal TotalInsurance { get; set; }
        public decimal TotalNetPayable { get; set; }
        public string Status { get; set; } = "Draft";
        public bool IsFinalized { get; set; }
    }

    public class PayrollDetailsViewModel
    {
        public int Id { get; set; }
        public int Year { get; set; }
        public int Month { get; set; }
        public string Status { get; set; } = "Draft";
        public bool IsFinalized { get; set; }
        public decimal TotalBaseSalary { get; set; }
        public decimal TotalInsurance { get; set; }
        public decimal TotalNetPayable { get; set; }
        public decimal PreviousMonthTotalNetPayable { get; set; }
        public decimal NetPayableDelta { get; set; }
        public List<PayrollChangeSummaryVM> ChangeSummary { get; set; } = new();
        public List<PayrollEmployeeRowViewModel> Items { get; set; } = new();
    }

    public class PayrollCalculationRequestViewModel
    {
        [Range(1, 12, ErrorMessage = "ماه باید بین 1 تا 12 باشد.")]
        public int Month { get; set; }

        [Range(1300, 1600, ErrorMessage = "سال وارد شده معتبر نیست.")]
        public int Year { get; set; }
    }

    public class PayrollSaveRequestViewModel
    {
        [Range(1, 12, ErrorMessage = "ماه باید بین 1 تا 12 باشد.")]
        public int Month { get; set; }

        [Range(1300, 1600, ErrorMessage = "سال وارد شده معتبر نیست.")]
        public int Year { get; set; }

        public bool Finalize { get; set; }

        public List<PayrollEmployeeRowViewModel> Items { get; set; } = new();
    }

    public class PayrollEmployeeRowViewModel
    {
        public int? Id { get; set; }

        public int? PayrollListId { get; set; }

        public int? HumanCapitalEmployeeId { get; set; }

        [Required(ErrorMessage = "نام کارمند الزامی است.")]
        [StringLength(120)]
        public string EmployeeName { get; set; } = string.Empty;

        [StringLength(100)]
        public string? JobTitle { get; set; }

        [StringLength(20)]
        public string? HireDateShamsi { get; set; }

        [Range(0, 99999999999)]
        public decimal BaseSalary { get; set; }

        [Range(0, 99999999999)]
        public decimal Allowance { get; set; }

        [Range(0, 99999999999)]
        public decimal Overtime { get; set; }

        [Range(0, 99999999999)]
        public decimal InsuranceDeduction { get; set; }

        [Range(0, 99999999999)]
        public decimal Tax { get; set; }

        [Range(0, 99999999999)]
        public decimal NetPayable { get; set; }

        public bool IsLockedFromHr { get; set; }
    }

    public class PayrollQualityWarningVM
    {
        public string Title { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string Tone { get; set; } = "warning";
    }

    public class PayrollChangeSummaryVM
    {
        public string Label { get; set; } = string.Empty;

        public string Value { get; set; } = string.Empty;

        public string Tone { get; set; } = "primary";
    }
}
