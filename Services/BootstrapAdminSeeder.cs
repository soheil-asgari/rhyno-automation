using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Models;
using OfficeAutomation.Modules.Identity.Infrastructure.Persistence;

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
        var email = _configuration.GetValue<string>("BootstrapAdmin:Email") ?? "admin";
        var password = _configuration.GetValue<string>("BootstrapAdmin:Password") ?? "admin";
        var fullName = _configuration.GetValue<string>("BootstrapAdmin:FullName") ?? "مدیر سیستم";

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
            // اگر پسورد ساده بود و Identity رد کرد،
            // با پسورد موقت قوی می‌سازیم و بعد هش admin را جایگزین می‌کنیم
            var tempPassword = Guid.NewGuid().ToString("N") + "Aa@1";
            result = await _userManager.CreateAsync(adminUser, tempPassword);
            if (!result.Succeeded)
            {
                var errors = string.Join("; ", result.Errors.Select(e => $"[{e.Code}] {e.Description}"));
                _logger.LogError("BootstrapAdmin: failed to create admin: {Errors}", errors);
                throw new InvalidOperationException($"Bootstrap admin creation failed: {errors}");
            }

            var passwordHasher = new PasswordHasher<User>();
            adminUser.PasswordHash = passwordHasher.HashPassword(adminUser, password);
            _dbContext.Users.Update(adminUser);
            await _dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("BootstrapAdmin: password set directly (bypassed strict policy).");
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
    }
}
