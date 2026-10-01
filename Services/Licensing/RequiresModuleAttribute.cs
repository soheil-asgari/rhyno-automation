using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace OfficeAutomation.Services.Licensing;

/// <summary>
/// Action and Controller filter attribute that restricts access to endpoints
/// based on the active license's enabled modules (from ILicenseContext).
/// If the requested module is not enabled in the active license, requests are
/// redirected to a user-friendly "Module Not Licensed" page (or 403 JSON for API calls).
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public class RequiresModuleAttribute : Attribute, IAsyncActionFilter
{
    public string[] ModuleNames { get; }
    public string ModuleName => ModuleNames.Length > 0 ? ModuleNames[0] : string.Empty;

    public RequiresModuleAttribute(string moduleName)
    {
        if (string.IsNullOrWhiteSpace(moduleName))
            throw new ArgumentException("Module name cannot be null or empty.", nameof(moduleName));
        ModuleNames = [moduleName];
    }

    public RequiresModuleAttribute(params string[] moduleNames)
    {
        if (moduleNames == null || moduleNames.Length == 0)
            throw new ArgumentException("At least one module name must be specified.", nameof(moduleNames));
        ModuleNames = moduleNames;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var licenseContext = context.HttpContext.RequestServices.GetService<ILicenseContext>();

        // If license context is not registered or unavailable, proceed
        if (licenseContext == null)
        {
            await next();
            return;
        }

        // Check if any of the required modules is enabled in the active license
        var isEnabled = ModuleNames.Any(m => licenseContext.IsModuleEnabled(m));

        if (!isEnabled)
        {
            var isApiOrJson = context.HttpContext.Request.Headers["Accept"].ToString().Contains("application/json", StringComparison.OrdinalIgnoreCase)
                || string.Equals(context.HttpContext.Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase)
                || context.HttpContext.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase);

            var primaryModule = ModuleName;

            if (isApiOrJson)
            {
                context.Result = new JsonResult(new
                {
                    success = false,
                    isLicensed = false,
                    module = primaryModule,
                    message = $"ماژول '{GetModuleDisplayName(primaryModule)}' در لایسنس فعال شما وجود ندارد.",
                    redirectUrl = $"/Home/ModuleNotLicensed?module={Uri.EscapeDataString(primaryModule)}"
                })
                {
                    StatusCode = StatusCodes.Status403Forbidden
                };
                return;
            }

            var routeValues = new RouteValueDictionary
            {
                ["module"] = primaryModule
            };

            // Preserve tab context if within tab frame
            if (context.HttpContext.Request.Query.ContainsKey("_tab"))
            {
                routeValues["_tab"] = context.HttpContext.Request.Query["_tab"].ToString();
            }

            context.Result = new RedirectToActionResult("ModuleNotLicensed", "Home", routeValues);
            return;
        }

        await next();
    }

    public static string GetModuleDisplayName(string moduleName)
    {
        return LicensableModules.GetDisplayName(moduleName);
    }
}
