using OfficeAutomation.Models;

namespace OfficeAutomation.Services.Security;

public sealed record AbacResourceContext(
    string ResourceType,
    string Action,
    string? OwnerUserId = null,
    int? DepartmentId = null,
    bool ContainsSensitiveData = false,
    IReadOnlyDictionary<string, string?>? Attributes = null);

public sealed record AbacAuthorizationResult(bool Allowed, string? Reason = null);

public interface IAbacAuthorizationService
{
    Task<AbacAuthorizationResult> AuthorizeAsync(PermissionAccessProfile? profile, AbacResourceContext resourceContext, CancellationToken cancellationToken = default);
}

public sealed class AbacAuthorizationService : IAbacAuthorizationService
{
    public Task<AbacAuthorizationResult> AuthorizeAsync(PermissionAccessProfile? profile, AbacResourceContext resourceContext, CancellationToken cancellationToken = default)
    {
        if (profile == null)
        {
            return Task.FromResult(new AbacAuthorizationResult(false, "Unauthenticated principal."));
        }

        if (profile.HasGlobalAccess)
        {
            return Task.FromResult(new AbacAuthorizationResult(true));
        }

        if (resourceContext.DepartmentId.HasValue &&
            profile.DepartmentId.HasValue &&
            resourceContext.DepartmentId.Value != profile.DepartmentId.Value)
        {
            return Task.FromResult(new AbacAuthorizationResult(false, "Cross-department access is denied."));
        }

        if (resourceContext.Action.Equals("Approve", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(resourceContext.OwnerUserId) &&
            string.Equals(resourceContext.OwnerUserId, profile.UserId, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(new AbacAuthorizationResult(false, "Segregation of duties policy blocks self-approval."));
        }

        if (resourceContext.ContainsSensitiveData &&
            !profile.Permissions.Contains($"{resourceContext.ResourceType}.ViewSensitive") &&
            !profile.Permissions.Contains("Security.Manage"))
        {
            return Task.FromResult(new AbacAuthorizationResult(false, "Sensitive-data clearance is missing."));
        }

        return Task.FromResult(new AbacAuthorizationResult(true));
    }
}
