using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using OfficeAutomation.Modules.Identity.Infrastructure.Persistence;
using OfficeAutomation.Models;
using System.Globalization;

namespace OfficeAutomation.Services.Security
{
    public interface IPermissionAccessService
    {
        Task<bool> UserHasPermissionAsync(string userId, string permission, CancellationToken cancellationToken = default);
        Task<PermissionAccessProfile?> GetAccessProfileAsync(string userId, CancellationToken cancellationToken = default);
        Task InvalidateUserAsync(string userId);
    }

    public sealed class PermissionAccessService : IPermissionAccessService
    {
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);
        private readonly IdentityDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly Services.Tenancy.ITenantCacheKeyProvider? _tenantCacheKeyProvider;

        public PermissionAccessService(IdentityDbContext context, IMemoryCache cache, Services.Tenancy.ITenantCacheKeyProvider? tenantCacheKeyProvider = null)
        {
            _context = context;
            _cache = cache;
            _tenantCacheKeyProvider = tenantCacheKeyProvider;
        }

        public async Task<bool> UserHasPermissionAsync(string userId, string permission, CancellationToken cancellationToken = default)
        {
            var profile = await GetAccessProfileAsync(userId, cancellationToken);
            return profile != null && profile.Permissions.Contains(permission);
        }

        public async Task<PermissionAccessProfile?> GetAccessProfileAsync(string userId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return null;
            }

            var cacheKey = _tenantCacheKeyProvider?.Prefix($"rbac:profile:{userId}") ?? $"rbac:profile:{userId}";
            if (_cache.TryGetValue(cacheKey, out PermissionAccessProfile? cachedProfile))
            {
                return cachedProfile;
            }

            var user = await _context.Users
                .AsNoTracking()
                .Where(item => item.Id == userId)
                .Select(item => new
                {
                    item.Id,
                    DisplayName = string.IsNullOrWhiteSpace(item.FullName) ? (item.UserName ?? item.Email ?? item.Id) : item.FullName,
                    item.DepartmentId
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (user == null)
            {
                return null;
            }

            var roles = await (
                from userRole in _context.UserRoles.AsNoTracking()
                join role in _context.Roles.AsNoTracking() on userRole.RoleId equals role.Id
                where userRole.UserId == userId
                select new
                {
                    role.Id,
                    RoleName = role.Name ?? role.Id,
                    role.DataAccessScope
                })
                .ToListAsync(cancellationToken);

            var roleIds = roles.Select(item => item.Id).Distinct().ToList();
            var permissions = roleIds.Count == 0
                ? []
                : await _context.RolePermissions
                    .AsNoTracking()
                    .Where(item => roleIds.Contains(item.RoleId) && item.IsAllowed)
                    .Select(item => item.PermissionKey)
                    .Distinct()
                    .ToListAsync(cancellationToken);

            var profile = new PermissionAccessProfile
            {
                UserId = user.Id,
                DisplayName = user.DisplayName,
                DepartmentId = user.DepartmentId,
                HasGlobalAccess = roles.Any(item => string.Equals(item.DataAccessScope, RoleDataAccessScope.Global, StringComparison.OrdinalIgnoreCase)),
                Clearance = permissions.Contains("Security.Manage", StringComparer.OrdinalIgnoreCase) ||
                            permissions.Contains("AuditLogs.Read", StringComparer.OrdinalIgnoreCase)
                    ? "Privileged"
                    : permissions.Any(item => item.EndsWith(".ViewSensitive", StringComparison.OrdinalIgnoreCase))
                        ? "Sensitive"
                        : "Standard",
                Roles = roles.Select(item => item.RoleName).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(item => item).ToList(),
                Permissions = new HashSet<string>(permissions, StringComparer.OrdinalIgnoreCase),
                Attributes = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["departmentId"] = user.DepartmentId?.ToString(CultureInfo.InvariantCulture),
                    ["clearance"] = permissions.Contains("Security.Manage", StringComparer.OrdinalIgnoreCase) ? "Privileged" : "Standard"
                }
            };

            _cache.Set(cacheKey, profile, CacheDuration);
            return profile;
        }

        public Task InvalidateUserAsync(string userId)
        {
            _cache.Remove(_tenantCacheKeyProvider?.Prefix($"rbac:profile:{userId}") ?? $"rbac:profile:{userId}");
            return Task.CompletedTask;
        }
    }
}
