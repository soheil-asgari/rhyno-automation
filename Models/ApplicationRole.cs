using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace OfficeAutomation.Models
{
    public class ApplicationRole : IdentityRole
    {
        [StringLength(256)]
        public string? Description { get; set; }

        [Required]
        [StringLength(32)]
        public string DataAccessScope { get; set; } = RoleDataAccessScope.Department;
    }

    public static class RoleDataAccessScope
    {
        public const string Global = "Global";
        public const string Department = "Department";

        public static readonly string[] All = [Global, Department];
    }
}
