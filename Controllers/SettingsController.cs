using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OfficeAutomation.Models;
using System.Threading.Tasks;
using System;

namespace OfficeAutomation.Controllers
{
    public class SettingsController : Controller
    {
        private readonly UserManager<User> _userManager;

        public SettingsController(UserManager<User> userManager)
        {
            _userManager = userManager;
        }

        // ۱. نمایش صفحه تنظیمات
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            return View(user);
        }

        // ۲. متد ذخیره امضا به صورت رشته Base64 (بسیار سریع و بدون نیاز به آپلود فایل پیچیده)
        [HttpPost]
        public async Task<IActionResult> SaveSignature([FromBody] SignatureUploadModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Json(new { success = false, message = "کاربر یافت نشد" });

            if (string.IsNullOrEmpty(model.ImageData))
                return Json(new { success = false, message = "داده تصویر خالی است" });

            try
            {
                // ذخیره مستقیم رشته تصویر در دیتابیس (فیلد SignaturePath)
                user.SignaturePath = model.ImageData;
                var result = await _userManager.UpdateAsync(user);

                if (result.Succeeded)
                {
                    return Json(new { success = true, message = "امضا با موفقیت ذخیره شد" });
                }
                return Json(new { success = false, message = "خطا در بروزرسانی دیتابیس" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }
    }

    // مدل کمکی برای دریافت داده‌های امضا
    public class SignatureUploadModel
    {
        public string ImageData { get; set; }
    }
}