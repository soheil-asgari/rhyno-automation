using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Models;
using OfficeAutomation.Modules.Identity.Infrastructure.Persistence;

namespace OfficeAutomation.Services.Licensing;

public sealed class LicenseUserEnforcementService : ILicenseUserEnforcementService
{
    private readonly ILicenseContext _licenseContext;
    private readonly IdentityDbContext _identityDbContext;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly UserManager<User> _userManager;

    // Standard mappings from module name to Permission key prefixes / keys
    private static readonly Dictionary<string, string[]> ModulePermissionPrefixes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Contracts"] = ["Contract.", "Notification.AlertSettings"],
        ["Financial"] = ["Finance."],
        ["Warehouse"] = ["Warehouse."],
        ["HumanCapital"] = ["HR."]
    };

    public LicenseUserEnforcementService(
        ILicenseContext licenseContext,
        IdentityDbContext identityDbContext,
        RoleManager<ApplicationRole> roleManager,
        UserManager<User> userManager)
    {
        _licenseContext = licenseContext;
        _identityDbContext = identityDbContext;
        _roleManager = roleManager;
        _userManager = userManager;
    }

    public async Task<LicenseUserValidationResult> ValidateUserCreationOrRoleAssignmentAsync(
        string? existingUserId,
        IEnumerable<string> targetRoleNames,
        CancellationToken cancellationToken = default)
    {
        if (_licenseContext.IsLocked || _licenseContext.CurrentLicense == null)
        {
            // If license context is locked or missing, let base license enforcement handle or block
            return LicenseUserValidationResult.Success();
        }

        var license = _licenseContext.CurrentLicense;
        var roleNamesList = targetRoleNames
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Select(r => r.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // 1. Check Global Limit if this is a brand new user
        var now = DateTimeOffset.UtcNow;
        if (string.IsNullOrWhiteSpace(existingUserId))
        {
            var totalActiveUsers = await _identityDbContext.Users
                .AsNoTracking()
                .Where(u => !u.LockoutEnabled || u.LockoutEnd == null || u.LockoutEnd <= now)
                .CountAsync(cancellationToken);

            if (license.MaxUsers > 0 && totalActiveUsers >= license.MaxUsers)
            {
                return LicenseUserValidationResult.LimitReached(
                    "Global",
                    totalActiveUsers,
                    license.MaxUsers,
                    $"شما به سقف مجاز کل کاربران سامانه رسیده‌اید: {totalActiveUsers} از {license.MaxUsers}");
            }
        }

        if (roleNamesList.Count == 0)
        {
            return LicenseUserValidationResult.Success();
        }

        // 2. Identify which modules these target roles grant permissions to
        var roles = await _roleManager.Roles
            .AsNoTracking()
            .Where(r => roleNamesList.Contains(r.Name!))
            .Select(r => new { r.Id, r.Name })
            .ToListAsync(cancellationToken);

        var roleIds = roles.Select(r => r.Id).ToList();

        // Get permissions assigned to these target roles
        var targetPermissions = await _identityDbContext.RolePermissions
            .AsNoTracking()
            .Where(rp => roleIds.Contains(rp.RoleId) && rp.IsAllowed)
            .Select(rp => rp.PermissionKey)
            .Distinct()
            .ToListAsync(cancellationToken);

        // Check each licensed module
        var licensedModules = license.Modules.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        foreach (var mod in licensedModules)
        {
            var grantsModule = RoleGrantsModuleAccess(roles.Select(r => r.Name!), targetPermissions, mod);
            if (!grantsModule)
            {
                continue;
            }

            var limitForMod = license.GetMaxUsersForModule(mod);
            if (limitForMod <= 0) continue;

            // Get users currently having access to this module
            var activeUsersInModule = await GetActiveUserIdsInModuleAsync(mod, cancellationToken);

            // If user already counted in this module, adding this role doesn't increase count
            if (!string.IsNullOrWhiteSpace(existingUserId) && activeUsersInModule.Contains(existingUserId))
            {
                continue;
            }

            var currentCount = activeUsersInModule.Count;
            if (currentCount >= limitForMod)
            {
                var modDisplayName = RequiresModuleAttribute.GetModuleDisplayName(mod);
                return LicenseUserValidationResult.LimitReached(
                    mod,
                    currentCount,
                    limitForMod,
                    $"شما به سقف مجاز کاربران این ماژول ({modDisplayName}) رسیده‌اید: {currentCount} از {limitForMod}");
            }
        }

        return LicenseUserValidationResult.Success();
    }

    public async Task<IReadOnlyList<ModuleUserUsage>> GetModuleUserUsagesAsync(CancellationToken cancellationToken = default)
    {
        var result = new List<ModuleUserUsage>();
        var license = _licenseContext.CurrentLicense;
        if (license == null)
        {
            return result;
        }

        var now = DateTimeOffset.UtcNow;
        var totalActiveUsers = await _identityDbContext.Users
            .AsNoTracking()
            .Where(u => !u.LockoutEnabled || u.LockoutEnd == null || u.LockoutEnd <= now)
            .CountAsync(cancellationToken);

        result.Add(new ModuleUserUsage
        {
            ModuleName = "Global",
            ModuleDisplayName = "کل کاربران سامانه",
            ActiveUserCount = totalActiveUsers,
            MaxUsersAllowed = license.MaxUsers
        });

        foreach (var mod in license.Modules.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var userIds = await GetActiveUserIdsInModuleAsync(mod, cancellationToken);
            var limit = license.GetMaxUsersForModule(mod);

            result.Add(new ModuleUserUsage
            {
                ModuleName = mod,
                ModuleDisplayName = RequiresModuleAttribute.GetModuleDisplayName(mod),
                ActiveUserCount = userIds.Count,
                MaxUsersAllowed = limit
            });
        }

        return result;
    }

    public async Task<IReadOnlyList<ModuleUserUsage>> GetDowngradedExceededModulesAsync(CancellationToken cancellationToken = default)
    {
        var usages = await GetModuleUserUsagesAsync(cancellationToken);
        return usages.Where(u => u.IsExceeded).ToList();
    }

    private async Task<HashSet<string>> GetActiveUserIdsInModuleAsync(string moduleName, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;

        // 1. Get all active user IDs
        var activeUserIds = await _identityDbContext.Users
            .AsNoTracking()
            .Where(u => !u.LockoutEnabled || u.LockoutEnd == null || u.LockoutEnd <= now)
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);

        if (activeUserIds.Count == 0)
        {
            return [];
        }

        var activeUserSet = new HashSet<string>(activeUserIds, StringComparer.OrdinalIgnoreCase);

        // 2. Find roles that have permissions for this module
        var prefixes = GetModulePermissionPrefixes(moduleName);

        var allowedRolePermissions = await _identityDbContext.RolePermissions
            .AsNoTracking()
            .Where(rp => rp.IsAllowed)
            .Select(rp => new { rp.RoleId, rp.PermissionKey })
            .ToListAsync(cancellationToken);

        var matchingRoleIds = allowedRolePermissions
            .Where(rp => prefixes.Any(p => rp.PermissionKey.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            .Select(rp => rp.RoleId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Also check if role is "Admin" (Admin role inherently accesses all modules)
        var adminRole = await _roleManager.FindByNameAsync("Admin");
        if (adminRole != null && !matchingRoleIds.Contains(adminRole.Id))
        {
            matchingRoleIds.Add(adminRole.Id);
        }

        // Special check for role names that inherently belong to this module (e.g. FinanceManager)
        if (string.Equals(moduleName, "Financial", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(moduleName, "Finance", StringComparison.OrdinalIgnoreCase))
        {
            var finRole = await _roleManager.FindByNameAsync("FinanceManager");
            if (finRole != null && !matchingRoleIds.Contains(finRole.Id))
            {
                matchingRoleIds.Add(finRole.Id);
            }
        }
        else if (string.Equals(moduleName, "Warehouse", StringComparison.OrdinalIgnoreCase))
        {
            var whRole = await _roleManager.FindByNameAsync("WarehouseManager");
            if (whRole != null && !matchingRoleIds.Contains(whRole.Id))
            {
                matchingRoleIds.Add(whRole.Id);
            }
        }
        else if (string.Equals(moduleName, "HumanCapital", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(moduleName, "HR", StringComparison.OrdinalIgnoreCase))
        {
            var hrRole = await _roleManager.FindByNameAsync("HrManager");
            if (hrRole != null && !matchingRoleIds.Contains(hrRole.Id))
            {
                matchingRoleIds.Add(hrRole.Id);
            }
        }

        // 3. User IDs mapped to matching roles
        var usersWithMatchingRoles = await _identityDbContext.UserRoles
            .AsNoTracking()
            .Where(ur => matchingRoleIds.Contains(ur.RoleId))
            .Select(ur => ur.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var userIdsInModule = new HashSet<string>(usersWithMatchingRoles.Where(id => activeUserSet.Contains(id)), StringComparer.OrdinalIgnoreCase);

        // 4. Also check direct user flags on User entity for backward compatibility (CanAccessFinance, etc.)
        if (string.Equals(moduleName, "Financial", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(moduleName, "Finance", StringComparison.OrdinalIgnoreCase))
        {
            var directUsers = await _identityDbContext.Users
                .AsNoTracking()
                .Where(u => u.CanAccessFinance && (!u.LockoutEnabled || u.LockoutEnd == null || u.LockoutEnd <= now))
                .Select(u => u.Id)
                .ToListAsync(cancellationToken);

            foreach (var du in directUsers) userIdsInModule.Add(du);
        }
        else if (string.Equals(moduleName, "Warehouse", StringComparison.OrdinalIgnoreCase))
        {
            var directUsers = await _identityDbContext.Users
                .AsNoTracking()
                .Where(u => u.CanAccessWarehouse && (!u.LockoutEnabled || u.LockoutEnd == null || u.LockoutEnd <= now))
                .Select(u => u.Id)
                .ToListAsync(cancellationToken);

            foreach (var du in directUsers) userIdsInModule.Add(du);
        }
        else if (string.Equals(moduleName, "HumanCapital", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(moduleName, "HR", StringComparison.OrdinalIgnoreCase))
        {
            var directUsers = await _identityDbContext.Users
                .AsNoTracking()
                .Where(u => u.CanAccessHumanCapital && (!u.LockoutEnabled || u.LockoutEnd == null || u.LockoutEnd <= now))
                .Select(u => u.Id)
                .ToListAsync(cancellationToken);

            foreach (var du in directUsers) userIdsInModule.Add(du);
        }

        return userIdsInModule;
    }

    private static bool RoleGrantsModuleAccess(IEnumerable<string> roleNames, IEnumerable<string> rolePermissions, string moduleName)
    {
        // Admin role grants access to all modules
        if (roleNames.Any(r => string.Equals(r, "Admin", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        var prefixes = GetModulePermissionPrefixes(moduleName);
        if (rolePermissions.Any(perm => prefixes.Any(p => perm.StartsWith(p, StringComparison.OrdinalIgnoreCase))))
        {
            return true;
        }

        // Check specific standard role names
        if ((string.Equals(moduleName, "Financial", StringComparison.OrdinalIgnoreCase) || string.Equals(moduleName, "Finance", StringComparison.OrdinalIgnoreCase))
            && roleNames.Any(r => string.Equals(r, "FinanceManager", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (string.Equals(moduleName, "Warehouse", StringComparison.OrdinalIgnoreCase)
            && roleNames.Any(r => string.Equals(r, "WarehouseManager", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if ((string.Equals(moduleName, "HumanCapital", StringComparison.OrdinalIgnoreCase) || string.Equals(moduleName, "HR", StringComparison.OrdinalIgnoreCase))
            && roleNames.Any(r => string.Equals(r, "HrManager", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return false;
    }

    private static string[] GetModulePermissionPrefixes(string moduleName)
    {
        if (string.Equals(moduleName, "Contracts", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(moduleName, "Contract", StringComparison.OrdinalIgnoreCase))
        {
            return ModulePermissionPrefixes["Contracts"];
        }

        if (string.Equals(moduleName, "Financial", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(moduleName, "Finance", StringComparison.OrdinalIgnoreCase))
        {
            return ModulePermissionPrefixes["Financial"];
        }

        if (string.Equals(moduleName, "Warehouse", StringComparison.OrdinalIgnoreCase))
        {
            return ModulePermissionPrefixes["Warehouse"];
        }

        if (string.Equals(moduleName, "HumanCapital", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(moduleName, "HR", StringComparison.OrdinalIgnoreCase))
        {
            return ModulePermissionPrefixes["HumanCapital"];
        }

        return [moduleName + "."];
    }
}
