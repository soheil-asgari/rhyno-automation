using System.Security.Claims;

namespace OfficeAutomation.Services.Security
{
    public interface ICurrentUserContextAccessor
    {
        string? UserId { get; }
        Task<PermissionAccessProfile?> GetAccessProfileAsync(CancellationToken cancellationToken = default);
    }

    public sealed class CurrentUserContextAccessor : ICurrentUserContextAccessor
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IPermissionAccessService _permissionAccessService;

        public CurrentUserContextAccessor(
            IHttpContextAccessor httpContextAccessor,
            IPermissionAccessService permissionAccessService)
        {
            _httpContextAccessor = httpContextAccessor;
            _permissionAccessService = permissionAccessService;
        }

        public string? UserId => _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);

        public Task<PermissionAccessProfile?> GetAccessProfileAsync(CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(UserId))
            {
                return Task.FromResult<PermissionAccessProfile?>(null);
            }

            return _permissionAccessService.GetAccessProfileAsync(UserId, cancellationToken);
        }
    }
}
