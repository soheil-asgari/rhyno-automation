using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Data;
using Microsoft.AspNetCore.Authorization;

namespace OfficeAutomation.Filters
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class RequireAccessAreaAttribute : Attribute
    {
        public RequireAccessAreaAttribute(string area)
        {
            Area = area;
        }

        public string Area { get; }
    }

    public class PermissionAccessFilter : IAsyncActionFilter
    {
        private readonly ApplicationDbContext _context;

        public PermissionAccessFilter(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            if (context.Filters.OfType<IAllowAnonymousFilter>().Any() ||
                context.ActionDescriptor.EndpointMetadata.OfType<AllowAnonymousAttribute>().Any())
            {
                await next();
                return;
            }

            var user = context.HttpContext.User;
            if (user?.Identity?.IsAuthenticated != true)
            {
                await next();
                return;
            }

            if (user.IsInRole("Admin"))
            {
                await next();
                return;
            }

            var areaRequirement = context.ActionDescriptor.EndpointMetadata
                .OfType<RequireAccessAreaAttribute>()
                .FirstOrDefault();

            var area = areaRequirement?.Area ?? ResolveAreaByController(context.RouteData.Values["controller"]?.ToString());
            if (string.IsNullOrWhiteSpace(area))
            {
                await next();
                return;
            }

            var userId = user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(userId))
            {
                context.Result = new ForbidResult();
                return;
            }

            var permissionData = await _context.Users
                .AsNoTracking()
                .Where(item => item.Id == userId)
                .Select(item => new
                {
                    item.CanAccessFinance,
                    item.CanAccessWarehouse,
                    item.CanAccessHumanCapital,
                    item.CanAccessSystemSettings
                })
                .FirstOrDefaultAsync(context.HttpContext.RequestAborted);

            if (permissionData == null)
            {
                context.Result = new ForbidResult();
                return;
            }

            var allowed = area switch
            {
                "Finance" => permissionData.CanAccessFinance,
                "Warehouse" => permissionData.CanAccessWarehouse,
                "HumanCapital" => permissionData.CanAccessHumanCapital,
                "SystemSettings" => permissionData.CanAccessSystemSettings,
                _ => true
            };

            if (!allowed)
            {
                context.Result = new RedirectToActionResult("AccessDenied", "Home", null);
                return;
            }

            await next();
        }

        private static string? ResolveAreaByController(string? controller)
        {
            return controller switch
            {
                "Financial" or "Payroll" or "Bimeh" => "Finance",
                "Warehouse" or "Vendors" or "Employers" => "Warehouse",
                "HumanCapital" => "HumanCapital",
                "Settings" => "SystemSettings",
                _ => null
            };
        }
    }
}
