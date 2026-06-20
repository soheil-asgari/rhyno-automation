namespace OfficeAutomation.Services.Security;

public interface IAuthorizationFacade
{
    Task<bool> HasPermissionAsync(string permission, CancellationToken cancellationToken = default);
    Task<bool> IsSecurityAdminAsync(CancellationToken cancellationToken = default);
}

public sealed class AuthorizationFacade : IAuthorizationFacade
{
    private readonly ICurrentUserContextAccessor _currentUser;
    private readonly IPermissionAccessService _permissionAccessService;

    public AuthorizationFacade(ICurrentUserContextAccessor currentUser, IPermissionAccessService permissionAccessService)
    {
        _currentUser = currentUser;
        _permissionAccessService = permissionAccessService;
    }

    public async Task<bool> HasPermissionAsync(string permission, CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserId;
        return !string.IsNullOrWhiteSpace(userId) &&
               await _permissionAccessService.UserHasPermissionAsync(userId, permission, cancellationToken);
    }

    public async Task<bool> IsSecurityAdminAsync(CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserId;
        if (string.IsNullOrWhiteSpace(userId))
        {
            return false;
        }

        var profile = await _currentUser.GetAccessProfileAsync(cancellationToken);
        return profile?.HasGlobalAccess == true ||
               profile?.Permissions.Contains("Security.Manage") == true ||
               profile?.Permissions.Contains("Permissions.Manage") == true;
    }
}
