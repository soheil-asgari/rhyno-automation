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

        public string Gender { get; set; } // قبلاً احتمالاً int بوده، حتماً string کن


    }
}