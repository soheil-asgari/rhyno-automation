using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.RateLimiting;
using System.Globalization;
using System.Threading.RateLimiting;
using OfficeAutomation.Data;
using OfficeAutomation.Filters;
using OfficeAutomation.Models;
using OfficeAutomation.Services;
using OfficeAutomation.Services.Auditing;
using OfficeAutomation.Services.Security;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// 1. تنظیمات دیتابیس
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// 2. تنظیمات Identity (اصلاح شده برای پشتیبانی از نقش‌ها)
builder.Services.AddIdentity<User, ApplicationRole>(options => {
    options.Password.RequireDigit = false;
    options.Password.RequiredLength = 4;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = false;

    // تنظیمات مهم برای نام کاربری به جای ایمیل
    options.User.AllowedUserNameCharacters =
        "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+ " +
        "آابپتثجچحخدذرزژسشصضطظعغفقکگلمنوهی";

    options.User.RequireUniqueEmail = false;
    options.SignIn.RequireConfirmedAccount = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders()
.AddDefaultUI();

builder.Services.AddSingleton<AiService>();
builder.Services.AddMemoryCache();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IAuditContextProvider, HttpAuditContextProvider>();
builder.Services.AddScoped<IPermissionAccessService, PermissionAccessService>();
builder.Services.AddScoped<ICurrentUserContextAccessor, CurrentUserContextAccessor>();
builder.Services.AddScoped<IAuthorizationFacade, AuthorizationFacade>();
builder.Services.AddScoped<IDataIsolationService, DataIsolationService>();
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionAuthorizationPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddAuthorization();

builder.Services.AddScoped<PermissionAccessFilter>();

builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add<NormalizeInputFilter>();
    options.Filters.Add<PermissionAccessFilter>();
});
builder.Services.AddRazorPages();
builder.Services.AddScoped<LeaveWorkflowService>();
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database");
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.User?.Identity?.IsAuthenticated == true
                ? context.User.Identity!.Name ?? "authenticated-user"
                : context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 120,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});

builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var faCulture = new CultureInfo("fa-IR");
    var enCulture = new CultureInfo("en-US");
    options.DefaultRequestCulture = new RequestCulture(faCulture);
    options.SupportedCultures = [faCulture, enCulture];
    options.SupportedUICultures = [faCulture, enCulture];
});

var app = builder.Build();
var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");

// 3. تنظیمات محیط اجرا
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseRequestLocalization();
app.UseRateLimiter();
app.Use(async (context, next) =>
{
    var start = DateTimeOffset.UtcNow;
    try
    {
        await next();
    }
    finally
    {
        var elapsed = DateTimeOffset.UtcNow - start;
        if (elapsed.TotalMilliseconds >= 500)
        {
            logger.LogInformation(
                "Slow request {Method} {Path} responded {StatusCode} in {ElapsedMs}ms",
                context.Request.Method,
                context.Request.Path,
                context.Response.StatusCode,
                elapsed.TotalMilliseconds.ToString("F0", CultureInfo.InvariantCulture));
        }
    }
});

// 4. ترتیب حیاتی احراز هویت
app.UseAuthentication();
app.UseAuthorization();

// 5. نقشه مسیرها
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();
app.MapHealthChecks("/health");

// 6. خودکارسازی دیتابیس و ساخت نقش و کاربر ادمین (کاملاً ناهمگام و ایمن)
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();

        // اصلاح خط اول: مهاجرت به صورت کاملاً Async انجام شود تا خللی در ساخت جداول پیش نیاید
        await context.Database.MigrateAsync();

        var userManager = services.GetRequiredService<UserManager<User>>();
        var roleManager = services.GetRequiredService<RoleManager<ApplicationRole>>();

        var bootstrapRoles = new[] { "Admin", "FinanceManager", "WarehouseManager", "HrManager" };
        foreach (var roleName in bootstrapRoles)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                await roleManager.CreateAsync(new ApplicationRole
                {
                    Name = roleName,
                    DataAccessScope = string.Equals(roleName, "Admin", StringComparison.OrdinalIgnoreCase)
                        ? RoleDataAccessScope.Global
                        : RoleDataAccessScope.Department
                });
            }
        }

        var existingPermissions = await context.Permissions
            .AsNoTracking()
            .Select(item => item.Key)
            .ToListAsync();

        var missingPermissions = PermissionCatalog.CorePermissions
            .Where(item => !existingPermissions.Contains(item.Key, StringComparer.OrdinalIgnoreCase))
            .ToList();

        if (missingPermissions.Count != 0)
        {
            context.Permissions.AddRange(missingPermissions);
            await context.SaveChangesAsync();
        }

        // ب) ساخت یوزر ادمین
        var adminEmail = builder.Configuration["BootstrapAdmin:Email"] ?? "admin@alpha.com";
        var adminPassword = builder.Configuration["BootstrapAdmin:Password"];

        if (string.IsNullOrWhiteSpace(adminPassword))
        {
            logger.LogWarning("Bootstrap admin password is missing. Skipping admin user provisioning.");
        }
        else
        {
            var adminUser = await userManager.FindByEmailAsync(adminEmail);
            var bootstrapRole = await roleManager.FindByNameAsync("Admin");

            if (adminUser == null)
            {
                adminUser = new User
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    FullName = "مدیر سیستم",
                    EmailConfirmed = true
                };

                var createResult = await userManager.CreateAsync(adminUser, adminPassword);
                if (!createResult.Succeeded)
                {
                    logger.LogError(
                        "Failed to create bootstrap admin user {AdminEmail}: {Errors}",
                        adminEmail,
                        string.Join(" | ", createResult.Errors.Select(item => item.Description)));
                }
            }

            if (adminUser != null && !await userManager.IsInRoleAsync(adminUser, "Admin"))
            {
                var roleResult = await userManager.AddToRoleAsync(adminUser, "Admin");
                if (!roleResult.Succeeded)
                {
                    logger.LogError(
                        "Failed to assign Admin role to bootstrap user {AdminEmail}: {Errors}",
                        adminEmail,
                        string.Join(" | ", roleResult.Errors.Select(item => item.Description)));
                }
            }

            if (bootstrapRole != null)
            {
                bootstrapRole.DataAccessScope = RoleDataAccessScope.Global;
                var updateRoleResult = await roleManager.UpdateAsync(bootstrapRole);
                if (!updateRoleResult.Succeeded)
                {
                    logger.LogError(
                        "Failed to update Admin role scope: {Errors}",
                        string.Join(" | ", updateRoleResult.Errors.Select(item => item.Description)));
                }

                var permissionKeys = PermissionCatalog.CorePermissions.Select(item => item.Key).ToArray();
                foreach (var key in permissionKeys)
                {
                    var existing = await context.RolePermissions
                        .FirstOrDefaultAsync(item => item.RoleId == bootstrapRole.Id && item.PermissionKey == key);
                    if (existing == null)
                    {
                        context.RolePermissions.Add(new RolePermission
                        {
                            RoleId = bootstrapRole.Id,
                            PermissionKey = key,
                            IsAllowed = true
                        });
                    }
                    else if (!existing.IsAllowed)
                    {
                        existing.IsAllowed = true;
                    }
                }

                await context.SaveChangesAsync();
            }
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error during startup bootstrap and database initialization.");
    }
}

app.Run();

public partial class Program { }
