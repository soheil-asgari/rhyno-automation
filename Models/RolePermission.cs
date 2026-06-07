using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace OfficeAutomation.Models
{
    public class RolePermission
    {
        public int Id { get; set; }

        [Required]
        public string RoleId { get; set; } = string.Empty;

        public ApplicationRole? Role { get; set; }

        [Required]
        [StringLength(128)]
        public string PermissionKey { get; set; } = string.Empty;

        public Permission? Permission { get; set; }

        public bool IsAllowed { get; set; }
    }
}
