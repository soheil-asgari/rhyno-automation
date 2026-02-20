using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OfficeAutomation.Models;
using System;
using System.Linq; // برای Select و ToList الزامی است
using System.Threading.Tasks;
using System.Collections.Generic;

namespace OfficeAutomation.Controllers
{
    public class SettingsController : Controller
    {
        private readonly UserManager<User> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager; // اضافه شد

        // تزریق هر دو سرویس در سازنده
        public SettingsController(UserManager<User> userManager, RoleManager<IdentityRole> roleManager)
        {
            _userManager = userManager;
            _roleManager = roleManager;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();
            return View(user);
        }
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateUser(string email, string fullName, string jobTitle, string password, string role, string gender)
        {
            // ۱. چک کردن تکراری نبودن کاربر قبل از هر عملیاتی
            var existingUser = await _userManager.FindByEmailAsync(email);
            if (existingUser != null)
            {
                return Json(new { success = false, message = "این نام کاربری (ایمیل) قبلاً ثبت شده است." });
            }

            var user = new User
            {
                UserName = email,
                Email = email,
                FullName = fullName,
                JobTitle = jobTitle,
                Gender = gender,
                EmailConfirmed = true
            };

            // ۲. ساخت کاربر
            var result = await _userManager.CreateAsync(user, password);

            if (result.Succeeded)
            {
                // ۳. بخش حیاتی: بررسی و ساخت نقش (Role) اگر وجود نداشته باشد
                if (string.IsNullOrEmpty(role)) role = "User"; // پیش‌فرض

                if (!await _roleManager.RoleExistsAsync(role))
                {
                    await _roleManager.CreateAsync(new IdentityRole(role));
                }

                // ۴. اضافه کردن کاربر به نقش بدون ترس از کرش کردن
                await _userManager.AddToRoleAsync(user, role);

                return Json(new { success = true, message = "کاربر جدید با موفقیت ایجاد شد." });
            }

            // اگر به هر دلیل دات‌نت خطا گرفت (مثلاً پسورد ضعیف بود)
            var errors = string.Join(" | ", result.Errors.Select(e => e.Description));
            return Json(new { success = false, message = errors });
        }


        [HttpPost]
        public async Task<IActionResult> SaveSignature([FromBody] SignatureUploadModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Json(new { success = false, message = "کاربر یافت نشد" });
            if (string.IsNullOrEmpty(model.ImageData)) return Json(new { success = false, message = "داده تصویر خالی است" });

            try
            {
                user.SignaturePath = model.ImageData;
                var result = await _userManager.UpdateAsync(user);
                return Json(new { success = result.Succeeded });
            }
            catch (Exception ex) { return Json(new { success = false, message = ex.Message }); }
        }

        // مدیریت کاربران - مخصوص ادمین
        [Authorize(Roles = "Admin")]
        [HttpGet]
        public IActionResult GetAllUsers()
        {
            var users = _userManager.Users.Select(u => new {
                u.Id,
                u.FullName,
                u.UserName,
                u.JobTitle,
                u.Email
            }).ToList();
            return Json(users);
        }

     
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteUser(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return Json(new { success = false });

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser.Id == userId) return Json(new { success = false, message = "خودتان را نمی‌توانید حذف کنید." });

            var result = await _userManager.DeleteAsync(user);
            return Json(new { success = result.Succeeded });
        }

        // متد نجات‌بخش برای ادمین کردن اکانت شما
        [HttpGet]
        public async Task<string> CreateTestAdmin()
        {
            var roleName = "Admin";
            if (!await _roleManager.RoleExistsAsync(roleName))
                await _roleManager.CreateAsync(new IdentityRole(roleName));

            var user = await _userManager.FindByEmailAsync("admin@alpha.com");
            if (user != null)
            {
                await _userManager.AddToRoleAsync(user, roleName);
                return "تبریک! شما الان ادمین هستید. صفحه تنظیمات را رفرش کنید.";
            }
            return "کاربر admin@alpha.com پیدا نشد.";
        }
    }

    public class SignatureUploadModel { public string ImageData { get; set; } }
}