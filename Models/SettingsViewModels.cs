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
}
