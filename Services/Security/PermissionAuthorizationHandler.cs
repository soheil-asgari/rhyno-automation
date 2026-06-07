using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace OfficeAutomation.Services.Security
{
    public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
    {
        private readonly IPermissionAccessService _permissionAccessService;

        public PermissionAuthorizationHandler(IPermissionAccessService permissionAccessService)
        {
            _permissionAccessService = permissionAccessService;
        }

        protected override async Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            PermissionRequirement requirement)
        {
            if (context.User.Identity?.IsAuthenticated != true)
            {
                return;
            }

            var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
            {
                return;
            }

            if (await _permissionAccessService.UserHasPermissionAsync(userId, requirement.Permission))
            {
                context.Succeed(requirement);
            }
        }
    }
}
