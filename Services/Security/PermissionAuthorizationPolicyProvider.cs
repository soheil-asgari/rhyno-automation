using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace OfficeAutomation.Services.Security
{
    public sealed class PermissionAuthorizationPolicyProvider : DefaultAuthorizationPolicyProvider
    {
        public const string PolicyPrefix = "Permission:";

        public PermissionAuthorizationPolicyProvider(IOptions<AuthorizationOptions> options)
            : base(options)
        {
        }

        public override Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
        {
            if (policyName.StartsWith(PolicyPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var permission = policyName[PolicyPrefix.Length..];
                var policy = new AuthorizationPolicyBuilder()
                    .AddRequirements(new PermissionRequirement(permission))
                    .Build();
                return Task.FromResult<AuthorizationPolicy?>(policy);
            }

            return base.GetPolicyAsync(policyName);
        }
    }
}
