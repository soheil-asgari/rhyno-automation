using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace OfficeAutomation.Services.Licensing;

/// <summary>
/// Enforces license locks across application routes.
/// When the application is operating in Locked mode (missing/expired/invalid license),
/// all module routes are blocked except the license activation and settings endpoints,
/// static assets, authentication/login, and health checks.
/// </summary>
public sealed class LicenseEnforcementMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILicenseContext _licenseContext;
    private readonly ILogger<LicenseEnforcementMiddleware> _logger;

    private static readonly HashSet<string> AllowedPathPrefixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "/Settings",
        "/Identity",
        "/Account",
        "/health",
        "/lib",
        "/css",
        "/js",
        "/images",
        "/favicon.ico",
        "/swagger",
        "/ModuleNotLicensed",
        "/Home/ModuleNotLicensed"
    };

    public LicenseEnforcementMiddleware(
        RequestDelegate next,
        ILicenseContext licenseContext,
        ILogger<LicenseEnforcementMiddleware> logger)
    {
        _next = next;
        _licenseContext = licenseContext;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!_licenseContext.IsLocked)
        {
            await _next(context);
            return;
        }

        var path = context.Request.Path.Value ?? string.Empty;

        // Allow static files, authentication, health checks, and settings pages
        if (IsAllowedPath(path))
        {
            await _next(context);
            return;
        }

        // For AJAX / JSON API requests, respond with 423 Locked or 403 Forbidden with lock reason
        var isApiOrJson = context.Request.Headers["Accept"].ToString().Contains("application/json", StringComparison.OrdinalIgnoreCase)
                          || string.Equals(context.Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase)
                          || path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase);

        if (isApiOrJson)
        {
            context.Response.StatusCode = StatusCodes.Status423Locked;
            context.Response.ContentType = "application/json; charset=utf-8";
            await context.Response.WriteAsJsonAsync(new
            {
                success = false,
                isLocked = true,
                lockReason = _licenseContext.LockReason ?? "سیستم به دلیل عدم وجود لایسنس معتبر قفل می‌باشد.",
                failureReason = _licenseContext.ValidationResult.FailureReason?.ToString() ?? "Locked",
                activationUrl = "/Settings#license"
            }, context.RequestAborted);
            return;
        }

        // For regular web browser navigation, redirect to Settings page where activation can take place
        _logger.LogWarning("Blocking request to {Path} due to license lock: {Reason}", path, _licenseContext.LockReason);
        context.Response.Redirect("/Settings?licenseLocked=true#license");
    }

    private static bool IsAllowedPath(string path)
    {
        if (string.IsNullOrEmpty(path) || path == "/")
        {
            return false;
        }

        foreach (var prefix in AllowedPathPrefixes)
        {
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
