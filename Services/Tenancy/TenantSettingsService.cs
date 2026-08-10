using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Modules.Platform.Infrastructure.Persistence;
using OfficeAutomation.Models;

namespace OfficeAutomation.Services.Tenancy;

public interface ITenantSettingsService
{
    Task<SystemSetting> GetSystemSettingsAsync(CancellationToken cancellationToken = default);
    Task UpdateSystemSettingsAsync(Action<SystemSetting> apply, CancellationToken cancellationToken = default);
    Task UpdateBrandingAsync(Action<SystemSetting> apply, CancellationToken cancellationToken = default);
}

public sealed class TenantSettingsService : ITenantSettingsService
{
    private readonly PlatformDbContext _context;
    private readonly ITenantIsolationService _tenantIsolationService;

    public TenantSettingsService(PlatformDbContext context, ITenantIsolationService tenantIsolationService)
    {
        _context = context;
        _tenantIsolationService = tenantIsolationService;
    }

    public async Task<SystemSetting> GetSystemSettingsAsync(CancellationToken cancellationToken = default)
    {
        var descriptor = _tenantIsolationService.GetCurrent();
        var baseSetting = await _context.SystemSettings.AsNoTracking().OrderBy(item => item.Id).FirstOrDefaultAsync(cancellationToken)
            ?? new SystemSetting();

        var overrides = await _context.TenantSettings
            .AsNoTracking()
            .Where(item => item.TenantId == descriptor.TenantId)
            .ToDictionaryAsync(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase, cancellationToken);

        return ApplyOverrides(baseSetting, overrides);
    }

    public async Task UpdateSystemSettingsAsync(Action<SystemSetting> apply, CancellationToken cancellationToken = default)
    {
        var descriptor = _tenantIsolationService.GetCurrent();
        var current = await GetSystemSettingsAsync(cancellationToken);
        apply(current);
        current.UpdatedAtUtc = DateTime.UtcNow;

        await UpsertAsync(descriptor.TenantId, "System.ApplicationTitle", current.ApplicationTitle, cancellationToken);
        await UpsertAsync(descriptor.TenantId, "System.SystemLanguage", current.SystemLanguage, cancellationToken);
        await UpsertAsync(descriptor.TenantId, "System.TimeZoneId", current.TimeZoneId, cancellationToken);
        await UpsertAsync(descriptor.TenantId, "System.ActiveEnvironment", current.ActiveEnvironment ?? string.Empty, cancellationToken);
        await UpsertAsync(descriptor.TenantId, "System.MaintenanceMode", current.MaintenanceMode.ToString(), cancellationToken);
    }

    /// <summary>
    /// Persists the White-labeling / Branding settings for the current tenant.
    /// These are stored as TenantSetting key-value pairs (prefixed "Branding.") so
    /// they can be loaded and injected as CSS Variables in the Layout without a
    /// separate database table or EF Core migration.
    /// </summary>
    public async Task UpdateBrandingAsync(Action<SystemSetting> apply, CancellationToken cancellationToken = default)
    {
        var descriptor = _tenantIsolationService.GetCurrent();
        var current = await GetSystemSettingsAsync(cancellationToken);
        apply(current);
        current.UpdatedAtUtc = DateTime.UtcNow;

        await UpsertAsync(descriptor.TenantId, "Branding.ApplicationTitle", current.ApplicationTitle ?? string.Empty, cancellationToken);
        await UpsertAsync(descriptor.TenantId, "Branding.PrimaryColor", current.PrimaryColor ?? string.Empty, cancellationToken);
        await UpsertAsync(descriptor.TenantId, "Branding.AccentColor", current.AccentColor ?? string.Empty, cancellationToken);
        await UpsertAsync(descriptor.TenantId, "Branding.LogoUrl", current.LogoUrl ?? string.Empty, cancellationToken);
        await UpsertAsync(descriptor.TenantId, "Branding.FaviconUrl", current.FaviconUrl ?? string.Empty, cancellationToken);
        await UpsertAsync(descriptor.TenantId, "Branding.LoginBackgroundUrl", current.LoginBackgroundUrl ?? string.Empty, cancellationToken);
        await UpsertAsync(descriptor.TenantId, "Branding.DesktopBackgroundUrl", current.DesktopBackgroundUrl ?? string.Empty, cancellationToken);
    }

    private async Task UpsertAsync(string tenantId, string key, string value, CancellationToken cancellationToken)
    {
        var row = await _context.TenantSettings.FirstOrDefaultAsync(item => item.TenantId == tenantId && item.Key == key, cancellationToken);
        if (row == null)
        {
            _context.TenantSettings.Add(new TenantSetting
            {
                TenantId = tenantId,
                Key = key,
                Value = value,
                UpdatedAtUtc = DateTime.UtcNow
            });
        }
        else
        {
            row.Value = value;
            row.UpdatedAtUtc = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    private static SystemSetting ApplyOverrides(SystemSetting setting, IReadOnlyDictionary<string, string> overrides)
    {
        var resolved = new SystemSetting
        {
            Id = setting.Id,
            ApplicationTitle = setting.ApplicationTitle,
            SystemLanguage = setting.SystemLanguage,
            TimeZoneId = setting.TimeZoneId,
            ActiveEnvironment = setting.ActiveEnvironment,
            MaintenanceMode = setting.MaintenanceMode,
            UpdatedAtUtc = setting.UpdatedAtUtc
        };

        if (overrides.TryGetValue("System.ApplicationTitle", out var title) && !string.IsNullOrWhiteSpace(title))
        {
            resolved.ApplicationTitle = title;
        }

        if (overrides.TryGetValue("Branding.ApplicationTitle", out var brandTitle) && !string.IsNullOrWhiteSpace(brandTitle))
        {
            resolved.ApplicationTitle = brandTitle;
        }

        if (overrides.TryGetValue("System.SystemLanguage", out var language) && !string.IsNullOrWhiteSpace(language))
        {
            resolved.SystemLanguage = language;
        }

        if (overrides.TryGetValue("System.TimeZoneId", out var timeZoneId) && !string.IsNullOrWhiteSpace(timeZoneId))
        {
            resolved.TimeZoneId = timeZoneId;
        }

        if (overrides.TryGetValue("System.ActiveEnvironment", out var activeEnvironment))
        {
            resolved.ActiveEnvironment = string.IsNullOrWhiteSpace(activeEnvironment) ? null : activeEnvironment;
        }

        if (overrides.TryGetValue("System.MaintenanceMode", out var maintenanceMode) &&
            bool.TryParse(maintenanceMode, out var parsedMaintenanceMode))
        {
            resolved.MaintenanceMode = parsedMaintenanceMode;
        }

        // ── White-labeling / Branding overrides ───────────────────────────────────
        if (overrides.TryGetValue("Branding.PrimaryColor", out var primaryColor) && !string.IsNullOrWhiteSpace(primaryColor))
        {
            resolved.PrimaryColor = primaryColor;
        }

        if (overrides.TryGetValue("Branding.AccentColor", out var accentColor) && !string.IsNullOrWhiteSpace(accentColor))
        {
            resolved.AccentColor = accentColor;
        }

        if (overrides.TryGetValue("Branding.LogoUrl", out var logoUrl) && !string.IsNullOrWhiteSpace(logoUrl))
        {
            resolved.LogoUrl = logoUrl;
        }

        if (overrides.TryGetValue("Branding.FaviconUrl", out var faviconUrl) && !string.IsNullOrWhiteSpace(faviconUrl))
        {
            resolved.FaviconUrl = faviconUrl;
        }

        if (overrides.TryGetValue("Branding.LoginBackgroundUrl", out var loginBg) && !string.IsNullOrWhiteSpace(loginBg))
        {
            resolved.LoginBackgroundUrl = loginBg;
        }

        if (overrides.TryGetValue("Branding.DesktopBackgroundUrl", out var desktopBg) && !string.IsNullOrWhiteSpace(desktopBg))
        {
            resolved.DesktopBackgroundUrl = desktopBg;
        }

        return resolved;
    }
}
