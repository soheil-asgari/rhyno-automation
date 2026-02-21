using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace OfficeAutomation.Models
{
    public enum Department
    {
        [Display(Name = "مالی")] Financial,
        [Display(Name = "اداری")] Administrative,
        [Display(Name = "فنی")] Technical,
        [Display(Name = "منابع انسانی")] HR,
        [Display(Name = "مدیریت")] Management
    }
    public class User : IdentityUser
    {
        [Required]
        public string? FullName { get; set; }

        // تغییر از internal به public برای ثبت در دیتابیس
        public string? JobTitle { get; set; }

        public string? Role { get; set; }

        public string? SignaturePath { get; set; }

        public string? Gender { get; set; }
        public string? ServiceLocation { get; set; }
        public string? ManagerId { get; set; }
        public virtual User? Manager { get; set; }
        public Department Department { get; set; }
        public bool IsManager { get; set; }



    }
}