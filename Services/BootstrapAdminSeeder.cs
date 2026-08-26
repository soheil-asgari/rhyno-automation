using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Models;
using OfficeAutomation.Modules.Identity.Infrastructure.Persistence;
using OfficeAutomation.Services.Security;

namespace OfficeAutomation.Services;

public class BootstrapAdminSeeder
{
    private readonly UserManager<User> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly IConfiguration _configuration;
    private readonly ILogger<BootstrapAdminSeeder> _logger;
    private readonly IdentityDbContext _dbContext;

    private const string DefUser = "admin";
    private const string DefPass = "admin";
    private const string DefEmail = "admin@localhost.local";
    private const string DefName = "مدیر سیستم";

    public BootstrapAdminSeeder(
        UserManager<User> userManager,
        RoleManager<ApplicationRole> roleManager,
        IConfiguration configuration,
        ILogger<BootstrapAdminSeeder> logger,
        IdentityDbContext dbContext)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _configuration = configuration;
        _logger = logger;
        _dbContext = dbContext;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        string username, password, email, fullName;
        var enabled = _configuration.GetValue("BootstrapAdmin:Enabled", false);

        if (enabled)
        {
            username = _configuration.GetValue<string>("BootstrapAdmin:Username") ?? DefUser;
            email = _configuration.GetValue<string>("BootstrapAdmin:Email") ?? DefEmail;
            password = _configuration.GetValue<string>("BootstrapAdmin:Password") ?? DefPass;
            fullName = _configuration.GetValue<string>("BootstrapAdmin:FullName") ?? DefName;
            if (string.IsNullOrWhiteSpace(password)) password = DefPass;
        }
        else
        {
            username = DefUser; email = DefEmail; password = DefPass; fullName = DefName;
        }

        _logger.LogInformation("BootstrapAdmin: checking...");
        await EnsureAdminRoleAsync(cancellationToken);

        var existing = await _userManager.FindByNameAsync(username);
        if (existing == null)
        {
            existing = await _userManager.FindByEmailAsync(email);
            // If found by email, also set UserName to match for login consistency
            if (existing != null && existing.UserName != username)
            {
                existing.UserName = username;
                existing.NormalizedUserName = _userManager.KeyNormalizer.NormalizeName(username);
                var setUserResult = await _userManager.UpdateAsync(existing);
                if (!setUserResult.Succeeded)
                    _logger.LogWarning("BootstrapAdmin: Could not update username for existing user by email.");
            }
        }

        if (existing != null)
        {
            _logger.LogInformation("BootstrapAdmin: admin already exists.");
            // Directly set a new password hash to bypass any password issues
            existing.PasswordHash = _userManager.PasswordHasher.HashPassword(existing, password);
            existing.SecurityStamp = Guid.NewGuid().ToString("D");
            existing.LockoutEnabled = false;
            existing.LockoutEnd = null;
            existing.AccessFailedCount = 0;
            existing.EmailConfirmed = true;
            var updateResult = await _userManager.UpdateAsync(existing);
            if (!updateResult.Succeeded)
                _logger.LogWarning("BootstrapAdmin: Could not update existing admin credentials: {Errors}",
                    string.Join("; ", updateResult.Errors.Select(e => e.Description)));
            await EnsureAdminPermissionsAsync(existing, cancellationToken);
            return;
        }

        _logger.LogInformation("BootstrapAdmin: creating admin user...");
        var admin = new User
        {
            UserName = username, Email = email, FullName = fullName,
            EmailConfirmed = true, PhoneNumberConfirmed = true,
            LockoutEnabled = false, TwoFactorEnabled = false,
            SecurityStamp = Guid.NewGuid().ToString("D"),
        };

        var cr = await _userManager.CreateAsync(admin, password);
        if (!cr.Succeeded) throw new InvalidOperationException("Create admin failed: " +
            string.Join("; ", cr.Errors.Select(e => e.Description)));

        var rr = await _userManager.AddToRoleAsync(admin, "Admin");
        if (!rr.Succeeded) throw new InvalidOperationException("Assign Admin role failed: " +
            string.Join("; ", rr.Errors.Select(e => e.Description)));

        await EnsureAdminPermissionsAsync(admin, cancellationToken);
        _logger.LogInformation("BootstrapAdmin: admin created.");
    }

    private async Task EnsureAdminRoleAsync(CancellationToken ct)
    {
        if (await _roleManager.RoleExistsAsync("Admin")) return;
        _logger.LogInformation("BootstrapAdmin: creating Admin role...");
        var r = await _roleManager.CreateAsync(new ApplicationRole
        { Name = "Admin", Description = "مدیر سیستم - دسترسی کامل",
          DataAccessScope = RoleDataAccessScope.Global });
        if (!r.Succeeded) throw new InvalidOperationException("Create Admin role failed");
        _logger.LogInformation("BootstrapAdmin: Admin role created.");
    }

    private async Task EnsureAdminPermissionsAsync(User adminUser, CancellationToken ct)
    {
        // Use UserManager to update user properties to avoid ConcurrencyStamp conflicts
        adminUser.CanAccessFinance = true;
        adminUser.CanAccessWarehouse = true;
        adminUser.CanAccessHumanCapital = true;
        adminUser.CanAccessSystemSettings = true;
        adminUser.IsManager = true;
        adminUser.Role = "Admin";
        adminUser.LockoutEnabled = false;
        adminUser.LockoutEnd = null;
        adminUser.AccessFailedCount = 0;

        var updateResult = await _userManager.UpdateAsync(adminUser);
        if (!updateResult.Succeeded)
            _logger.LogWarning("BootstrapAdmin: Update user properties had issues: {Errors}",
                string.Join("; ", updateResult.Errors.Select(e => e.Description)));

        // Also reset lockout if any
        await _userManager.ResetAccessFailedCountAsync(adminUser);
        await _userManager.SetLockoutEnabledAsync(adminUser, false);

        var adminRole = await _roleManager.FindByNameAsync("Admin");
        if (adminRole == null) return;
        foreach (var cp in PermissionCatalog.CorePermissions)
        {
            if (!await _dbContext.Permissions.AnyAsync(p => p.Key == cp.Key, ct))
                _dbContext.Permissions.Add(new Permission
                { Key = cp.Key, DisplayName = cp.DisplayName,
                  Category = cp.Category, Description = cp.Description,
                  IsSystem = true });
            if (!await _dbContext.RolePermissions.AnyAsync(
                rp => rp.RoleId == adminRole.Id && rp.PermissionKey == cp.Key, ct))
                _dbContext.RolePermissions.Add(new RolePermission
                { RoleId = adminRole.Id, PermissionKey = cp.Key, IsAllowed = true });
        }
        await _dbContext.SaveChangesAsync(ct);
        _logger.LogInformation("BootstrapAdmin: {Count} permissions synced.",
            PermissionCatalog.CorePermissions.Count);
    }
}