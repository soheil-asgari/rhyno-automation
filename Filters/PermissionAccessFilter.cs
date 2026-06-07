using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using OfficeAutomation.Services.Security;

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
        private readonly IAuthorizationService _authorizationService;

        public PermissionAccessFilter(IAuthorizationService authorizationService)
        {
            _authorizationService = authorizationService;
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

            var areaRequirement = context.ActionDescriptor.EndpointMetadata
                .OfType<RequireAccessAreaAttribute>()
                .FirstOrDefault();

            var permissions = ResolvePermissions(
                areaRequirement?.Area,
                context.RouteData.Values["controller"]?.ToString());

            if (permissions.Count == 0)
            {
                await next();
                return;
            }

            foreach (var permission in permissions)
            {
                var result = await _authorizationService.AuthorizeAsync(user, PermissionAuthorizationPolicyProvider.PolicyPrefix + permission);
                if (result.Succeeded)
                {
                    await next();
                    return;
                }
            }

            context.Result = new RedirectToActionResult("AccessDenied", "Home", null);
        }

        private static IReadOnlyList<string> ResolvePermissions(string? area, string? controller)
        {
            if (!string.IsNullOrWhiteSpace(area))
            {
                return area switch
                {
                    "Finance" => ["Finance.View"],
                    "Warehouse" => ["Warehouse.View"],
                    "HumanCapital" => ["HR.View"],
                    "SystemSettings" => ["SystemSettings.View"],
                    "WorkflowAdministration" => ["Security.Manage"],
                    _ => [area]
                };
            }

            if (!string.IsNullOrWhiteSpace(controller) &&
                PermissionCatalog.ControllerFallbackPermissions.TryGetValue(controller, out var permissions))
            {
                return permissions;
            }

            return [];
        }
    }
}
