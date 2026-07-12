using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using OfficeAutomation.Models;
using OfficeAutomation.Modules.Identity.Infrastructure.Persistence;
using OfficeAutomation.Services.Security;

namespace OfficeAutomation.Services;

public sealed class UserContextModel
{
    public string ThemePreference { get; set; } = "System";
    public bool CanAccessFinance { get; set; }
    public bool CanAccessWarehouse { get; set; }
    public bool CanAccessHumanCapital { get; set; }
    public bool CanAccessSystemSettings { get; set; }
    public bool CanAccessProcessAnalytics { get; set; }
    public string DisplayName { get; set; } = "کاربر";
    public string UserRoleLabel { get; set; } = "کاربر سیستم";
    public List<HeaderNotificationVM> Notifications { get; set; } = new();
}

public interface IUserContextService
{
    Task<UserContextModel> GetUserContextAsync(string userId, ClaimsPrincipal principal);
    void InvalidateCache(string userId);
}

public sealed class UserContextService : IUserContextService
{
    private readonly IMemoryCache _cache;
    private readonly IdentityDbContext _dbContext;
    private readonly IAuthorizationFacade _authorizationFacade;
    private readonly NotificationService _notificationService;

    public UserContextService(
        IMemoryCache cache,
        IdentityDbContext dbContext,
        IAuthorizationFacade authorizationFacade,
        NotificationService notificationService)
    {
        _cache = cache;
        _dbContext = dbContext;
        _authorizationFacade = authorizationFacade;
        _notificationService = notificationService;
    }

    public async Task<UserContextModel> GetUserContextAsync(string userId, ClaimsPrincipal principal)
    {
        var cacheKey = $"user_context_{userId}";
        if (_cache.TryGetValue<UserContextModel>(cacheKey, out var cachedModel) && cachedModel != null)
        {
            return cachedModel;
        }

        var model = new UserContextModel();

        // 1. Theme Preference
        var savedTheme = await _dbContext.UserPreferences
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .Select(p => p.ThemePreference)
            .FirstOrDefaultAsync();

        if (string.Equals(savedTheme, "Light", StringComparison.OrdinalIgnoreCase)) model.ThemePreference = "Light";
        else if (string.Equals(savedTheme, "Dark", StringComparison.OrdinalIgnoreCase)) model.ThemePreference = "Dark";

        // 2. Display Name
        model.DisplayName = principal.FindFirst("name")?.Value 
            ?? principal.FindFirst(ClaimTypes.Name)?.Value 
            ?? principal.Identity?.Name 
            ?? "کاربر";

        // 3. User Role Label & Security Admin check
        var userHasSecurityAccess = await _authorizationFacade.IsSecurityAdminAsync();
        model.UserRoleLabel = userHasSecurityAccess ? "مدیر سیستم" : "کاربر سیستم";
        if (!userHasSecurityAccess)
        {
            var roleClaim = principal.FindFirst(ClaimTypes.Role)?.Value ?? principal.FindFirst("role")?.Value;
            if (!string.IsNullOrWhiteSpace(roleClaim))
            {
                model.UserRoleLabel = roleClaim;
            }
        }

        // 4. Permissions
        model.CanAccessFinance = userHasSecurityAccess;
        model.CanAccessWarehouse = userHasSecurityAccess;
        model.CanAccessHumanCapital = userHasSecurityAccess;
        model.CanAccessSystemSettings = userHasSecurityAccess;
        model.CanAccessProcessAnalytics = userHasSecurityAccess;

        if (!userHasSecurityAccess)
        {
            var permission = await _dbContext.Users
                .AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => new { u.CanAccessFinance, u.CanAccessWarehouse, u.CanAccessHumanCapital, u.CanAccessSystemSettings })
                .FirstOrDefaultAsync();

            if (permission != null)
            {
                model.CanAccessFinance = permission.CanAccessFinance;
                model.CanAccessWarehouse = permission.CanAccessWarehouse;
                model.CanAccessHumanCapital = permission.CanAccessHumanCapital;
                model.CanAccessSystemSettings = permission.CanAccessSystemSettings;
            }
            model.CanAccessProcessAnalytics = await _authorizationFacade.HasPermissionAsync("تحلیل.مشاهده");
        }

        // 5. Notifications
        var headerNotifications = await _notificationService.GetHeaderNotificationsAsync(userId);
        model.Notifications = headerNotifications.ToList();

        // Cache for 5 minutes
        _cache.Set(cacheKey, model, TimeSpan.FromMinutes(5));

        return model;
    }

    public void InvalidateCache(string userId)
    {
        var cacheKey = $"user_context_{userId}";
        _cache.Remove(cacheKey);
    }
}
