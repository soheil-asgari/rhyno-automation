using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Models;
using OfficeAutomation.Modules.Identity.Infrastructure.Persistence;
using OfficeAutomation.Services.Security;

namespace OfficeAutomation.Services;

/// <summary>
/// روی استارتاپ اپلیکیشن، یک کاربر ادمین پیش‌فرض می‌سازد
/// اگر از قبل وجود نداشته باشد.
/// تنظیمات از بخش BootstrapAdmin در appsettings.json خوانده می‌شود.
/// </summary>
public class BootstrapAdminSeeder
{
    private readonly UserManager<User> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly IConfiguration _configuration;
    private readonly ILogger<BootstrapAdminSeeder> _logger;
    private readonly IdentityDbContext _dbContext;
    private readonly IWebHostEnvironment _env;

    public BootstrapAdminSeeder(
        UserManager<User> userManager,
        RoleManager<ApplicationRole> roleManager,
        IConfiguration configuration,
        ILogger<BootstrapAdminSeeder> logger,
        IdentityDbContext dbContext,
        IWebHostEnvironment env)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _configuration = configuration;
        _logger = logger;
        _dbContext = dbContext;
        _env = env;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        // ── Development-Only Guard ─────────────────────────────────────────────
        // The bootstrap admin is intended ONLY for local development/testing.
        // In Production it must NEVER auto-create a default super-admin, so we
        // fail fast (throw) rather than silently seeding credentials.
        if (!_env.IsDevelopment())
        {
            _logger.LogWarning("BootstrapAdmin: seeding is skipped because the environment is not Development.");
            return;
        }

        // Bootstrap admin is enabled only when explicitly requested via configuration.
        if (!_configuration.GetValue("BootstrapAdmin:Enabled", false))
        {
            _logger.LogInformation("BootstrapAdmin: disabled (BootstrapAdmin:Enabled is false).");
            return;
        }

        var email = _configuration.GetValue<string>("BootstrapAdmin:Email");
        var password = _configuration.GetValue<string>("BootstrapAdmin:Password");
        var fullName = _configuration.GetValue<string>("BootstrapAdmin:FullName") ?? "مدیر سیستم";

        // Fail fast (never fall back to a default) when credentials are missing.
        if (string.IsNullOrWhiteSpace(email))
            throw new InvalidOperationException("BootstrapAdmin:Email must be configured when BootstrapAdmin is enabled.");
        if (string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException("BootstrapAdmin:Password must be configured (no default fallback) when BootstrapAdmin is enabled.");

        // Reject weak development passwords so they cannot be promoted to production credentials.
        if (password.Length < 12)
            throw new InvalidOperationException("BootstrapAdmin:Password is too weak. A strong password (>= 12 characters) is required.");

        _logger.LogInformation("BootstrapAdmin: checking for existing '{Email}'...", email);

        await EnsureAdminRoleAsync(cancellationToken);

        var existingUser = await _userManager.FindByNameAsync(email)
            ?? await _userManager.FindByEmailAsync(email);

        if (existingUser != null)
        {
            _logger.LogInformation("BootstrapAdmin: admin '{Email}' already exists, skipping.", email);

            if (!await _userManager.IsInRoleAsync(existingUser, "Admin"))
            {
                await _userManager.AddToRoleAsync(existingUser, "Admin");
            }

            await EnsureAdminPermissionsAsync(existingUser, cancellationToken);
            return;
        }

        _logger.LogInformation("BootstrapAdmin: creating admin user '{Email}'...", email);

        var adminUser = new User
        {
            UserName = email,
            Email = email.Contains('@') ? email : $"{email}@rhyno.local",
            FullName = fullName,
            Role = "Admin",
            JobTitle = "مدیر سیستم",
            CanAccessFinance = true,
            CanAccessWarehouse = true,
            CanAccessHumanCapital = true,
            CanAccessSystemSettings = true,
            IsManager = true,
            EmailConfirmed = true
        };

        var result = await _userManager.CreateAsync(adminUser, password);

        if (!result.Succeeded)
        {
            // A valid bootstrap admin must satisfy the project's password policy.
            // We do NOT bypass the strict policy by overwriting the PasswordHash.
            var errors = string.Join("; ", result.Errors.Select(e => $"[{e.Code}] {e.Description}"));
            _logger.LogError("BootstrapAdmin: failed to create admin with configured password: {Errors}", errors);
            throw new InvalidOperationException(
                "Bootstrap admin creation failed: the configured BootstrapAdmin:Password does not satisfy the password policy. " +
                "Provide a strong password (>= 12 chars with upper, lower, digit, symbol).");
        }

        await _userManager.AddToRoleAsync(adminUser, "Admin");
        await EnsureAdminPermissionsAsync(adminUser, cancellationToken);

        _logger.LogInformation("BootstrapAdmin: admin user '{Email}' fully seeded.", email);
    }

    private async Task EnsureAdminRoleAsync(CancellationToken cancellationToken)
    {
        if (await _roleManager.RoleExistsAsync("Admin"))
            return;

        _logger.LogInformation("BootstrapAdmin: creating 'Admin' role...");
        var role = new ApplicationRole
        {
            Name = "Admin",
            Description = "مدیر سیستم - دسترسی کامل",
            DataAccessScope = RoleDataAccessScope.Global
        };

        var result = await _roleManager.CreateAsync(role);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => $"[{e.Code}] {e.Description}"));
            _logger.LogError("BootstrapAdmin: failed to create Admin role: {Errors}", errors);
            throw new InvalidOperationException($"Failed to create Admin role: {errors}");
        }

        _logger.LogInformation("BootstrapAdmin: 'Admin' role created.");
    }

    private async Task EnsureAdminPermissionsAsync(User adminUser, CancellationToken cancellationToken)
    {
        adminUser.CanAccessFinance = true;
        adminUser.CanAccessWarehouse = true;
        adminUser.CanAccessHumanCapital = true;
        adminUser.CanAccessSystemSettings = true;
        adminUser.IsManager = true;
        adminUser.Role = "Admin";

        _dbContext.Users.Update(adminUser);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // ── Seed ALL permissions from PermissionCatalog to the Permissions table & Admin role ────
        // This runs on every startup (not just first-run), so existing databases are
        // also covered — idempotent by design (no duplicates, no deletions).
        // This ensures the Admin role has EVERY registered permission, including Settings.*,
        // Security.*, Contracts.*, Finance.*, Warehouse.*, HR.*, etc.
        var adminRole = await _roleManager.FindByNameAsync("Admin");
        if (adminRole != null)
        {
            foreach (var corePerm in PermissionCatalog.CorePermissions)
            {
                // 1. Ensure the permission exists in the Permissions table (for UI role management)
                var permExists = await _dbContext.Permissions
                    .AnyAsync(p => p.Key == corePerm.Key, cancellationToken);

                if (!permExists)
                {
                    _dbContext.Permissions.Add(new Permission
                    {
                        Key = corePerm.Key,
                        DisplayName = corePerm.DisplayName,
                        Category = corePerm.Category,
                        Description = corePerm.Description,
                        IsSystem = true
                    });
                }

                // 2. Ensure the Admin role has this permission in RolePermissions
                var rpExists = await _dbContext.RolePermissions
                    .AnyAsync(rp => rp.RoleId == adminRole.Id && rp.PermissionKey == corePerm.Key, cancellationToken);

                if (!rpExists)
                {
                    _dbContext.RolePermissions.Add(new RolePermission
                    {
                        RoleId = adminRole.Id,
                        PermissionKey = corePerm.Key,
                        IsAllowed = true
                    });
                }
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("BootstrapAdmin: All {Count} permissions from PermissionCatalog synchronized to Permissions table and Admin role.", PermissionCatalog.CorePermissions.Count);
        }
    }
}
