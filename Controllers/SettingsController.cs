using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Data;
using OfficeAutomation.Models;
using OfficeAutomation.Services.Security;

namespace OfficeAutomation.Controllers;

[Authorize]
public class SettingsController : Controller
{
    private const string DefaultApplicationTitle = "Rhyno Dashboard";
    private const string DefaultLanguage = "fa-IR";
    private const string DefaultTimeZone = "Asia/Tehran";

    private static readonly HashSet<string> SupportedLanguages = new(StringComparer.OrdinalIgnoreCase)
    {
        "fa-IR",
        "en-US",
        "ar-SA"
    };

    private static readonly HashSet<string> SupportedThemes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Light",
        "Dark",
        "System"
    };

    private readonly ApplicationDbContext _context;
    private readonly UserManager<User> _userManager;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<SettingsController> _logger;
    private readonly IAuthorizationService _authorizationService;

    public SettingsController(
        ApplicationDbContext context,
        UserManager<User> userManager,
        IWebHostEnvironment environment,
        ILogger<SettingsController> logger,
        IAuthorizationService authorizationService)
    {
        _context = context;
        _userManager = userManager;
        _environment = environment;
        _logger = logger;
        _authorizationService = authorizationService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var viewModel = await BuildSettingsViewModelAsync(cancellationToken);
        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize("Security.Manage")]
    public async Task<IActionResult> UpdateGeneral([Bind(Prefix = "General")] GeneralSettingsViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            var vmWithErrors = await BuildSettingsViewModelAsync(cancellationToken);
            vmWithErrors.General = model;
            return View("Index", vmWithErrors);
        }

        if (!SupportedLanguages.Contains(model.SystemLanguage))
        {
            ModelState.AddModelError(nameof(model.SystemLanguage), "زبان انتخاب شده معتبر نیست.");
        }

        if (!IsValidTimeZone(model.TimeZoneId))
        {
            ModelState.AddModelError(nameof(model.TimeZoneId), "منطقه زمانی انتخاب شده معتبر نیست.");
        }

        if (!ModelState.IsValid)
        {
            var vmWithErrors = await BuildSettingsViewModelAsync(cancellationToken);
            vmWithErrors.General = model;
            return View("Index", vmWithErrors);
        }

        var systemSetting = await _context.SystemSettings.FirstOrDefaultAsync(cancellationToken);
        if (systemSetting == null)
        {
            systemSetting = new SystemSetting();
            _context.SystemSettings.Add(systemSetting);
        }

        systemSetting.ApplicationTitle = model.ApplicationTitle.Trim();
        systemSetting.SystemLanguage = model.SystemLanguage;
        systemSetting.TimeZoneId = model.TimeZoneId;
        systemSetting.ActiveEnvironment = _environment.EnvironmentName;
        systemSetting.UpdatedAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "General settings updated by {UserId}. Title={ApplicationTitle}, Language={Language}, TimeZone={TimeZone}",
            _userManager.GetUserId(User),
            systemSetting.ApplicationTitle,
            systemSetting.SystemLanguage,
            systemSetting.TimeZoneId);

        TempData["SettingsSuccess"] = "تنظیمات عمومی با موفقیت ذخیره شد.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateProfile([Bind(Prefix = "Profile")] ProfileSettingsViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            var vmWithErrors = await BuildSettingsViewModelAsync(cancellationToken);
            vmWithErrors.Profile = model;
            return View("Index", vmWithErrors);
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound();
        }

        user.FullName = model.FullName.Trim();
        user.Email = model.Email.Trim();
        user.UserName = model.Email.Trim();
        user.JobTitle = model.JobTitle?.Trim();
        user.PhoneNumber = model.PhoneNumber?.Trim();
        user.ServiceLocation = model.ServiceLocation?.Trim();

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            var vmWithErrors = await BuildSettingsViewModelAsync(cancellationToken);
            vmWithErrors.Profile = model;
            return View("Index", vmWithErrors);
        }

        _logger.LogInformation("Profile settings updated by {UserId}.", user.Id);
        TempData["SettingsSuccess"] = "پروفایل شما با موفقیت بروزرسانی شد.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword([Bind(Prefix = "Password")] ChangePasswordViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            var vmWithErrors = await BuildSettingsViewModelAsync(cancellationToken);
            vmWithErrors.Password = model;
            return View("Index", vmWithErrors);
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound();
        }

        var result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            var vmWithErrors = await BuildSettingsViewModelAsync(cancellationToken);
            vmWithErrors.Password = new ChangePasswordViewModel();
            return View("Index", vmWithErrors);
        }

        _logger.LogInformation("Password changed for user {UserId}.", user.Id);
        TempData["SettingsSuccess"] = "رمز عبور با موفقیت تغییر کرد.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize("Security.Manage")]
    public async Task<IActionResult> UpdateSystem([Bind(Prefix = "System")] SystemConnectivitySettingsViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            var vmWithErrors = await BuildSettingsViewModelAsync(cancellationToken);
            vmWithErrors.System = model;
            return View("Index", vmWithErrors);
        }

        var systemSetting = await _context.SystemSettings.FirstOrDefaultAsync(cancellationToken);
        if (systemSetting == null)
        {
            systemSetting = new SystemSetting();
            _context.SystemSettings.Add(systemSetting);
        }

        systemSetting.MaintenanceMode = model.MaintenanceMode;
        systemSetting.ActiveEnvironment = _environment.EnvironmentName;
        systemSetting.UpdatedAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogWarning(
            "System settings updated by {UserId}. MaintenanceMode={MaintenanceMode}, Environment={Environment}",
            _userManager.GetUserId(User),
            systemSetting.MaintenanceMode,
            systemSetting.ActiveEnvironment);

        TempData["SettingsSuccess"] = "تنظیمات اتصال و نگهداری با موفقیت ذخیره شد.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateUi([Bind(Prefix = "Ui")] UiPreferencesViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            var vmWithErrors = await BuildSettingsViewModelAsync(cancellationToken);
            vmWithErrors.Ui = model;
            return View("Index", vmWithErrors);
        }

        if (!SupportedThemes.Contains(model.ThemePreference))
        {
            ModelState.AddModelError(nameof(model.ThemePreference), "تم انتخاب شده معتبر نیست.");
            var vmWithErrors = await BuildSettingsViewModelAsync(cancellationToken);
            vmWithErrors.Ui = model;
            return View("Index", vmWithErrors);
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound();
        }

        var preference = await _context.UserPreferences.FirstOrDefaultAsync(p => p.UserId == user.Id, cancellationToken);
        if (preference == null)
        {
            preference = new UserPreference { UserId = user.Id };
            _context.UserPreferences.Add(preference);
        }

        preference.SidebarCollapsedByDefault = model.SidebarCollapsedByDefault;
        preference.ThemePreference = model.ThemePreference;
        preference.UpdatedAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "UI preferences updated by {UserId}. Theme={Theme}, SidebarCollapsed={SidebarCollapsed}",
            user.Id,
            preference.ThemePreference,
            preference.SidebarCollapsedByDefault);

        TempData["SettingsSuccess"] = "تنظیمات ظاهری با موفقیت ذخیره شد.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    [PermissionAuthorize("Security.Manage")]
    public IActionResult UserSignatureManagement()
    {
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    [PermissionAuthorize("Security.Manage")]
    public IActionResult AddNewUser()
    {
        return RedirectToAction("Create", "Users");
    }

    [HttpGet]
    [PermissionAuthorize("Security.Manage")]
    public IActionResult CreateUser()
    {
        return RedirectToAction("Create", "Users");
    }

    [HttpPost]
    [PermissionAuthorize("Security.Manage")]
    public async Task<IActionResult> SaveSignature([FromBody] SignatureUploadModel? model)
    {
        if (string.IsNullOrWhiteSpace(model?.ImageData))
        {
            return Json(new { success = false, message = "داده امضا ارسال نشده است." });
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Json(new { success = false, message = "کاربر یافت نشد." });
        }

        user.SignaturePath = model.ImageData;
        var result = await _userManager.UpdateAsync(user);

        if (!result.Succeeded)
        {
            var errors = string.Join(" | ", result.Errors.Select(error => error.Description));
            return Json(new { success = false, message = errors });
        }

        _logger.LogInformation("Signature updated by user {UserId}.", user.Id);
        return Json(new { success = true });
    }

    private async Task<SettingsIndexViewModel> BuildSettingsViewModelAsync(CancellationToken cancellationToken)
    {
        var canManageSecurity = (await _authorizationService.AuthorizeAsync(User, "Permission:Security.Manage")).Succeeded;
        var user = await _userManager.GetUserAsync(User);

        if (user == null)
        {
            return new SettingsIndexViewModel
            {
                CanManageSecurity = canManageSecurity,
                Languages = GetLanguageOptions(),
                TimeZones = GetTimeZoneOptions(),
                Themes = GetThemeOptions()
            };
        }

        var systemSetting = await _context.SystemSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        var userPreference = await _context.UserPreferences.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == user.Id, cancellationToken);
        var canConnectDb = await _context.Database.CanConnectAsync(cancellationToken);
        var resolvedSystem = systemSetting ?? BuildDefaultSystemSetting();

        return new SettingsIndexViewModel
        {
            General = new GeneralSettingsViewModel
            {
                ApplicationTitle = resolvedSystem.ApplicationTitle,
                SystemLanguage = resolvedSystem.SystemLanguage,
                TimeZoneId = resolvedSystem.TimeZoneId
            },
            Profile = new ProfileSettingsViewModel
            {
                FullName = user.FullName ?? string.Empty,
                Email = user.Email ?? user.UserName ?? string.Empty,
                JobTitle = user.JobTitle,
                PhoneNumber = user.PhoneNumber,
                ServiceLocation = user.ServiceLocation
            },
            Password = new ChangePasswordViewModel(),
            System = new SystemConnectivitySettingsViewModel
            {
                IsDatabaseConnected = canConnectDb,
                ActiveEnvironment = resolvedSystem.ActiveEnvironment ?? _environment.EnvironmentName,
                MaintenanceMode = resolvedSystem.MaintenanceMode,
                LastUpdatedUtc = resolvedSystem.UpdatedAtUtc
            },
            Ui = new UiPreferencesViewModel
            {
                SidebarCollapsedByDefault = userPreference?.SidebarCollapsedByDefault ?? false,
                ThemePreference = SupportedThemes.Contains(userPreference?.ThemePreference ?? string.Empty)
                    ? userPreference!.ThemePreference
                    : "System"
            },
            CurrentUserSignaturePath = user.SignaturePath,
            CanManageSecurity = canManageSecurity,
            Languages = GetLanguageOptions(),
            TimeZones = GetTimeZoneOptions(),
            Themes = GetThemeOptions()
        };
    }

    private static SystemSetting BuildDefaultSystemSetting()
    {
        return new SystemSetting
        {
            ApplicationTitle = DefaultApplicationTitle,
            SystemLanguage = DefaultLanguage,
            TimeZoneId = DefaultTimeZone,
            ActiveEnvironment = null,
            MaintenanceMode = false,
            UpdatedAtUtc = DateTime.UtcNow
        };
    }

    private static IReadOnlyCollection<SelectListItem> GetLanguageOptions()
    {
        return new List<SelectListItem>
        {
            new() { Text = "فارسی", Value = "fa-IR" },
            new() { Text = "English", Value = "en-US" },
            new() { Text = "العربية", Value = "ar-SA" }
        };
    }

    private static IReadOnlyCollection<SelectListItem> GetThemeOptions()
    {
        return new List<SelectListItem>
        {
            new() { Text = "روشن (Light)", Value = "Light" },
            new() { Text = "تیره (Dark)", Value = "Dark" },
            new() { Text = "سیستمی (System)", Value = "System" }
        };
    }

    private static IReadOnlyCollection<SelectListItem> GetTimeZoneOptions()
    {
        var preferred = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Asia/Tehran",
            "UTC",
            "Europe/Istanbul",
            "Asia/Dubai"
        };

        var list = TimeZoneInfo.GetSystemTimeZones()
            .Select(zone => new SelectListItem
            {
                Value = zone.Id,
                Text = $"{zone.DisplayName} ({zone.Id})"
            })
            .ToList();

        return list
            .OrderByDescending(item => preferred.Contains(item.Value ?? string.Empty))
            .ThenBy(item => item.Text)
            .ToList();
    }

    private static bool IsValidTimeZone(string timeZoneId)
    {
        return TimeZoneInfo.GetSystemTimeZones().Any(zone => string.Equals(zone.Id, timeZoneId, StringComparison.OrdinalIgnoreCase));
    }

    public sealed class SignatureUploadModel
    {
        public string? ImageData { get; set; }
    }
}
