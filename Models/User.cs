using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace OfficeAutomation.Models
{
    public class User : IdentityUser
    {
        [Required]
        public string? FullName { get; set; } // علامت سوال یعنی اجازه دارد تهی باشد

        public string? Role { get; set; }
        // دقت کن: دیگر نیازی به نوشتن Username و Password نیست، چون در IdentityUser وجود دارند.
        public string? SignaturePath { get; set; }
    }
}



