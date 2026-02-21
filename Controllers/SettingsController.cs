using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using OfficeAutomation.Models;
using System;
using System.Collections.Generic;
using System.Linq; // برای Select و ToList الزامی است
using System.Threading.Tasks;

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

            // اصلاح شد: لیست کاربران را کامل‌تر می‌گیریم تا در دراپ‌داون مدیران بهتر نمایش داده شود
            ViewBag.UsersList = _userManager.Users
                .Select(u => new User
                {
                    Id = u.Id,
                    FullName = u.FullName,
                    JobTitle = u.JobTitle,
                    UserName = u.UserName // اضافه شد
                }).ToList();

            return View(user);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateUser(UserViewModel model)
        {
            // ۱. چک کردن تکراری نبودن بر اساس ایمیل
            var existingUser = await _userManager.FindByEmailAsync(model.Email);
            if (existingUser != null)
            {
                return Json(new { success = false, message = "این ایمیل قبلاً ثبت شده است." });
            }

            // ۲. ایجاد شیء کاربر و انتساب مستقیم مقادیر
            var user = new User
            {
                UserName = model.Email,
                Email = model.Email,
                FullName = model.FullName,
                IsManager = model.IsManager,
                JobTitle = model.JobTitle,
                Gender = model.Gender,
                ServiceLocation = model.ServiceLocation,
                ManagerId = model.ManagerId,
                // چون در ویومدل نوع داده را اصلاح کردیم، اینجا انتساب مستقیم انجام می‌شود
                Department = model.Department,
                EmailConfirmed = true
            };

            // ۳. ساخت کاربر در دیتابیس
            var result = await _userManager.CreateAsync(user, model.Password);

            if (result.Succeeded)
            {
                // ۴. مدیریت نقش (Role)
                var role = string.IsNullOrEmpty(model.Role) ? "User" : model.Role;
                if (!await _roleManager.RoleExistsAsync(role))
                {
                    await _roleManager.CreateAsync(new IdentityRole(role));
                }
                await _userManager.AddToRoleAsync(user, role);

                return Json(new { success = true, message = "کاربر جدید با موفقیت ایجاد شد." });
            }

            var errors = string.Join(" | ", result.Errors.Select(e => e.Description));
            return Json(new { success = false, message = errors });
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateUser(UserViewModel model)
        {
            var user = await _userManager.FindByIdAsync(model.Id);
            if (user == null) return Json(new { success = false, message = "کاربر یافت نشد" });

            // بروزرسانی اطلاعات پایه
            user.FullName = model.FullName;
            user.JobTitle = model.JobTitle;
            user.Gender = model.Gender;
            user.ServiceLocation = model.ServiceLocation;
            user.IsManager = model.IsManager;
            user.ManagerId = model.ManagerId;
            user.Department = model.Department;

            var result = await _userManager.UpdateAsync(user);

            if (result.Succeeded)
            {
                // ۱. مدیریت تغییر رمز عبور (فقط یک بار)
                if (!string.IsNullOrEmpty(model.NewPassword))
                {
                    var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                    var passResult = await _userManager.ResetPasswordAsync(user, token, model.NewPassword);
                    if (!passResult.Succeeded)
                    {
                        return Json(new { success = false, message = "اطلاعات بروز شد اما خطا در تغییر رمز عبور: " + passResult.Errors.FirstOrDefault()?.Description });
                    }
                }

                // ۲. مدیریت نقش (Role)
                if (!string.IsNullOrEmpty(model.Role))
                {
                    var currentRoles = await _userManager.GetRolesAsync(user);

                    // اگر نقش فعلی با نقش جدید متفاوت است، بروزرسانی کن
                    if (!currentRoles.Contains(model.Role))
                    {
                        await _userManager.RemoveFromRolesAsync(user, currentRoles);

                        if (!await _roleManager.RoleExistsAsync(model.Role))
                            await _roleManager.CreateAsync(new IdentityRole(model.Role));

                        await _userManager.AddToRoleAsync(user, model.Role);
                    }
                }

                return Json(new { success = true, message = "تغییرات با موفقیت اعمال شد." });
            }

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
            var users = _userManager.Users.Select(u => new
            {
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

        [HttpGet]
        public async Task<IActionResult> GetUser(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var roles = await _userManager.GetRolesAsync(user);

            return Json(new
            {
                id = user.Id,
                fullName = user.FullName,
                userName = user.UserName,
                jobTitle = user.JobTitle,
                gender = user.Gender,
                location = user.ServiceLocation,
                department = (int)user.Department, // اصلاح شد به مقدار عددی
                isManager = user.IsManager,
                managerId = user.ManagerId,
                role = roles.FirstOrDefault() ?? "User"
            });
        }

        public class SignatureUploadModel { public string ImageData { get; set; } }
    }
}