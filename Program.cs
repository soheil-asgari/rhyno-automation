using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Data;
using OfficeAutomation.Filters;
using OfficeAutomation.Models;
using OfficeAutomation.Services;

var builder = WebApplication.CreateBuilder(args);

// 1. تنظیمات دیتابیس
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// 2. تنظیمات Identity (بهینه‌سازی شده برای تست راحت‌تر)
// 2. تنظیمات Identity (اصلاح شده برای پشتیبانی از نقش‌ها)
builder.Services.AddIdentity<User, IdentityRole>(options => {
    options.Password.RequireDigit = false;
    options.Password.RequiredLength = 4;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = false;

    // تنظیمات مهم برای نام کاربری به جای ایمیل
    options.User.AllowedUserNameCharacters =
        "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+ " +
        "آابپتثجچحخدذرزژسشصضطظعغفقکگلمنوهی";

    // تنظیمات مهم برای نام کاربری به جای ایمیل
    options.User.RequireUniqueEmail = false;
    options.SignIn.RequireConfirmedAccount = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders()
.AddDefaultUI();



builder.Services.AddSingleton<AiService>();

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
app.UseStaticFiles(); // برای خواندن فونت‌ها و فایل‌های CSS محلی

app.UseRouting(); // حتماً بعد از StaticFiles و قبل از Authentication

// 4. ترتیب حیاتی احراز هویت
app.UseAuthentication(); // شناسایی کاربر
app.UseAuthorization();  // بررسی سطح دسترسی

// 5. نقشه مسیرها
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();

// 6. خودکارسازی دیتابیس و ساخت کاربر ادمین برای اولین بار
// 6. خودکارسازی دیتابیس و ساخت نقش و کاربر ادمین
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        context.Database.Migrate();

        var userManager = services.GetRequiredService<UserManager<User>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

        // الف) ساخت نقش Admin اگر وجود ندارد
        if (!await roleManager.RoleExistsAsync("Admin"))
        {
            await roleManager.CreateAsync(new IdentityRole("Admin"));
        }

        // ب) ساخت یوزر ادمین
        var adminEmail = builder.Configuration["BootstrapAdmin:Email"] ?? "admin@alpha.com";
        var adminPassword = builder.Configuration["BootstrapAdmin:Password"];

        if (string.IsNullOrWhiteSpace(adminPassword) || adminPassword.Length < 12)
        {
            adminPassword = $"Adm!n-{Guid.NewGuid():N}".Substring(0, 16);
            Console.WriteLine("WARNING: Bootstrap admin password was missing/weak. Generated temporary secure password.");
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
        if (!await userManager.IsInRoleAsync(adminUser, "Admin"))
        {
            await userManager.AddToRoleAsync(adminUser, "Admin");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine("خطا در راه‌اندازی اولیه: " + ex.Message);
    }
}
app.Run();
