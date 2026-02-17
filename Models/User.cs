using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace OfficeAutomation.Models
{
    public class User : IdentityUser
    {
        [Required]
        public string? FullName { get; set; }

        // تغییر از internal به public برای ثبت در دیتابیس
        public string? JobTitle { get; set; }

        public string? Role { get; set; }

        public string? SignaturePath { get; set; }

        // فیلد جدید: 0=آقا، 1=خانم، 2=واحد سازمانی
        public int Gender { get; set; }
    }
}