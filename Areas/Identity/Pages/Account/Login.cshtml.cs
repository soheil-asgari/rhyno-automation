#nullable enable

using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OfficeAutomation.Models;

namespace OfficeAutomation.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class LoginModel : PageModel
    {
        private readonly SignInManager<User> _signInManager;
        private readonly ILogger<LoginModel> _logger;

        public LoginModel(SignInManager<User> signInManager, ILogger<LoginModel> logger)
        {
            _signInManager = signInManager;
            _logger = logger;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public IList<AuthenticationScheme> ExternalLogins { get; set; } = [];

        public string? ReturnUrl { get; set; }

        [TempData]
        public string? ErrorMessage { get; set; }

        public class InputModel
        {
            [Required(ErrorMessage = "لطفاً نام کاربری یا ایمیل خود را وارد کنید.")]
            [Display(Name = "نام کاربری یا ایمیل")]
            public string Email { get; set; } = string.Empty;

            [Required(ErrorMessage = "لطفاً رمز عبور را وارد کنید.")]
            [DataType(DataType.Password)]
            [Display(Name = "رمز عبور")]
            public string Password { get; set; } = string.Empty;

            [Display(Name = "مرا به خاطر بسپار")]
            public bool RememberMe { get; set; }
        }

        public async Task OnGetAsync(string? returnUrl = null)
        {
            if (!string.IsNullOrEmpty(ErrorMessage))
            {
                ModelState.AddModelError(string.Empty, ErrorMessage);
            }

            returnUrl ??= Url.Content("~/");
            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
            ReturnUrl = returnUrl;
        }

        public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
        {
            returnUrl = (string.IsNullOrEmpty(returnUrl) || !Url.IsLocalUrl(returnUrl)) 
                ? Url.Content("~/") 
                : returnUrl;

            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            if (!ModelState.IsValid)
            {
                return Page();
            }

            var identifier = Input.Email.Trim();
            var password = Input.Password.Trim();

            // جستجو هم بر اساس UserName و هم Email
            var user = await _signInManager.UserManager.FindByNameAsync(identifier)
                       ?? await _signInManager.UserManager.FindByEmailAsync(identifier);

            var resolvedUserName = user?.UserName ?? identifier;

            var result = await _signInManager.PasswordSignInAsync(
                resolvedUserName, 
                password, 
                Input.RememberMe, 
                lockoutOnFailure: true);

            if (result.Succeeded)
            {
                _logger.LogInformation("User '{UserName}' logged in successfully.", resolvedUserName);
                return LocalRedirect(returnUrl);
            }

            if (result.RequiresTwoFactor)
            {
                return RedirectToPage("./LoginWith2fa", new { ReturnUrl = returnUrl, RememberMe = Input.RememberMe });
            }

            if (result.IsLockedOut)
            {
                _logger.LogWarning("User account '{UserName}' locked out.", resolvedUserName);
                return RedirectToPage("./Lockout");
            }

            ModelState.AddModelError(string.Empty, "نام کاربری یا کلمه عبور وارد شده نادرست است.");
            return Page();
        }
    }
}