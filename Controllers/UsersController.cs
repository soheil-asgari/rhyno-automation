using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OfficeAutomation.Models;
using Microsoft.EntityFrameworkCore;

namespace OfficeAutomation.Controllers
{
    [Authorize] // فقط افراد وارد شده دسترسی داشته باشند
    public class UsersController : Controller
    {
        private readonly UserManager<User> _userManager;

        public UsersController(UserManager<User> userManager)
        {
            _userManager = userManager;
        }

        // ۱. نمایش لیست تمام کاربران
        public async Task<IActionResult> Index()
        {
            var users = await _userManager.Users.ToListAsync();
            return View(users);
        }

        // ۲. صفحه فرم ایجاد کاربر جدید (GET)
        public IActionResult Create()
        {
            return View();
        }

        // ۳. عملیات ذخیره کاربر جدید (POST)
        [HttpPost]
        public async Task<IActionResult> Create(string fullName, string email, string password)
        {
            if (ModelState.IsValid)
            {
                var user = new User
                {
                    UserName = email,
                    Email = email,
                    FullName = fullName,
                    EmailConfirmed = true // چون خودمان می‌سازیم تایید شده فرض می‌کنیم
                };

                var result = await _userManager.CreateAsync(user, password);

                if (result.Succeeded)
                {
                    TempData["SuccessMessage"] = "کاربر جدید با موفقیت تعریف شد.";
                    return RedirectToAction(nameof(Index));
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError("", error.Description);
                }
            }
            return View();
        }
    }
}