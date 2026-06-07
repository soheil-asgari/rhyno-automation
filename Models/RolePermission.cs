using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace OfficeAutomation.Models
{
    public class RolePermission
    {
        public int Id { get; set; }

        [Required]
        public string RoleId { get; set; } = string.Empty;

        public IdentityRole? Role { get; set; }

        [Required]
        [StringLength(80)]
        public string PermissionKey { get; set; } = string.Empty;

        public bool IsAllowed { get; set; }
    }
}
