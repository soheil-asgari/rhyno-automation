using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Data;
using OfficeAutomation.Models;
using Microsoft.AspNetCore.Identity;

var builder = WebApplication.CreateBuilder(args);

// 1. تنظیمات دیتابیس
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// 2. تنظیمات Identity (بهینه‌سازی شده برای تست راحت‌تر)
builder.Services.AddDefaultIdentity<User>(options => {
    options.Password.RequireDigit = false;
    options.Password.RequiredLength = 4;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = false;

    // تنظیمات مهم برای نام کاربری به جای ایمیل
    options.User.RequireUniqueEmail = false;
    options.SignIn.RequireConfirmedAccount = false;
})

.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

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
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        context.Database.Migrate(); // آپدیت خودکار دیتابیس

        var userManager = services.GetRequiredService<UserManager<User>>();

        // ساخت یوزر ادمین اگر وجود نداشته باشد
        var adminEmail = "admin@alpha.com";
        if (await userManager.FindByEmailAsync(adminEmail) == null)
        {
            var adminUser = new User
            {
                UserName = adminEmail,
                Email = adminEmail,
                FullName = "مدیر سیستم",
                EmailConfirmed = true
            };
            await userManager.CreateAsync(adminUser, "1234");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine("خطا در راه‌اندازی اولیه: " + ex.Message);
    }
}

app.Run();