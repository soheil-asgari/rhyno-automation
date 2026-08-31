#nullable enable

using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using OfficeAutomation.Models;

namespace OfficeAutomation.Areas.Identity.Pages.Account
{
    public class ForgotPasswordModel : PageModel
    {
        private readonly UserManager<User> _userManager;
        private readonly IEmailSender _emailSender;

        public ForgotPasswordModel(UserManager<User> userManager, IEmailSender emailSender)
        {
            _userManager = userManager;
            _emailSender = emailSender;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public class InputModel
        {
            [Required(ErrorMessage = "وارد کردن آدرس ایمیل الزامی است.")]
            [EmailAddress(ErrorMessage = "فرمت ایمیل وارد شده معتبر نیست.")]
            [Display(Name = "ایمیل")]
            public string Email { get; set; } = string.Empty;
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            var user = await _userManager.FindByEmailAsync(Input.Email.Trim());
            if (user == null || !(await _userManager.IsEmailConfirmedAsync(user)))
            {
                // جهت جلوگیری از Enumeration Attack مستقیم به تایید هدایت می‌شود
                return RedirectToPage("./ForgotPasswordConfirmation");
            }

            var code = await _userManager.GeneratePasswordResetTokenAsync(user);
            code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
            
            var callbackUrl = Url.Page(
                "/Account/ResetPassword",
                pageHandler: null,
                values: new { area = "Identity", code },
                protocol: Request.Scheme);

            if (callbackUrl != null)
            {
                await _emailSender.SendEmailAsync(
                    Input.Email,
                    "بازیابی کلمه عبور راینو ERP",
                    $"جهت بازنشانی کلمه عبور خود <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>اینجا کلیک کنید</a>.");
            }

            return RedirectToPage("./ForgotPasswordConfirmation");
        }
    }
}