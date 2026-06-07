using Microsoft.AspNetCore.Authorization;

namespace OfficeAutomation.Services.Security
{
    public sealed class PermissionAuthorizeAttribute : AuthorizeAttribute
    {
        public PermissionAuthorizeAttribute(string permission)
        {
            Permission = permission;
        }

        public string Permission
        {
            get => !string.IsNullOrWhiteSpace(Policy) && Policy.StartsWith(PermissionAuthorizationPolicyProvider.PolicyPrefix, StringComparison.Ordinal)
                ? Policy[PermissionAuthorizationPolicyProvider.PolicyPrefix.Length..]
                : (Policy ?? string.Empty);
            set => Policy = PermissionAuthorizationPolicyProvider.PolicyPrefix + value;
        }
    }
}
