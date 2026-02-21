using System.ComponentModel.DataAnnotations;

namespace OfficeAutomation.Models
{
    public class UserViewModel
    {
        // برای ویرایش حتما نیاز داریم
        public string? Id { get; set; }

        [Required(ErrorMessage = "نام کامل الزامی است")]
        public string FullName { get; set; }

        [Required(ErrorMessage = "ایمیل الزامی است")]
        [EmailAddress(ErrorMessage = "فرمت ایمیل صحیح نیست")]
        public string Email { get; set; }

        public string? JobTitle { get; set; }

        public string? PhoneNumber { get; set; } // فیلد جدید شماره تلفن

        // برای ساخت کاربر جدید اجباری، اما برای ویرایش اختیاری است
        public string? Password { get; set; }

        // برای ریست کردن رمز عبور توسط ادمین
        public string? NewPassword { get; set; }

        public Department Department { get; set; }

        public bool IsManager { get; set; }

        public string? ServiceLocation { get; set; }

        public string? Gender { get; set; }

        public string? Role { get; set; }

        public string? ManagerId { get; set; }
    }
}