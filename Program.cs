using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Data;
using OfficeAutomation.Filters;
using OfficeAutomation.Models;
using OfficeAutomation.Services;
using OfficeAutomation.Services.Auditing;
using OfficeAutomation.Services.Security;

var builder = WebApplication.CreateBuilder(args);

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

var app = builder.Build();

// 3. تنظیمات محیط اجرا
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// 4. ترتیب حیاتی احراز هویت
app.UseAuthentication();
app.UseAuthorization();

// 5. نقشه مسیرها
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();

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

        if (string.IsNullOrWhiteSpace(adminPassword) || adminPassword.Length < 12)
        {
            adminPassword = "DefaultSecurePass123!"; // یک پسورد ثابت و قوی لوکال برای دوقدم اول توسعه
            Console.WriteLine("WARNING: Bootstrap admin password was missing/weak. Used default secure password.");
        }

        var adminUser = await userManager.FindByEmailAsync(adminEmail);

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
                Console.WriteLine("خطا در ایجاد ادمین پیش‌فرض: " + string.Join(" | ", createResult.Errors.Select(item => item.Description)));
            }
        }

        // ج) مطمئن شویم یوزر ادمین، نقش Admin را دارد
        if (adminUser != null && !await userManager.IsInRoleAsync(adminUser, "Admin"))
        {
            await userManager.AddToRoleAsync(adminUser, "Admin");
        }

        var adminRole = await roleManager.FindByNameAsync("Admin");
        if (adminRole != null)
        {
            adminRole.DataAccessScope = RoleDataAccessScope.Global;
            await roleManager.UpdateAsync(adminRole);

            var permissionKeys = PermissionCatalog.CorePermissions.Select(item => item.Key).ToArray();
            foreach (var key in permissionKeys)
            {
                var existing = await context.RolePermissions
                    .FirstOrDefaultAsync(item => item.RoleId == adminRole.Id && item.PermissionKey == key);
                if (existing == null)
                {
                    context.RolePermissions.Add(new RolePermission
                    {
                        RoleId = adminRole.Id,
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
    catch (Exception ex)
    {
        Console.WriteLine("خطا در راه‌اندازی اولیه دیتابیس: " + ex.Message);
    }
}

app.Run();