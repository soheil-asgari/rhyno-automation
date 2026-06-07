using System.ComponentModel.DataAnnotations;

namespace OfficeAutomation.Models
{
    public class Permission
    {
        [Key]
        [StringLength(128)]
        public string Key { get; set; } = string.Empty;

        [Required]
        [StringLength(128)]
        public string DisplayName { get; set; } = string.Empty;

        [Required]
        [StringLength(64)]
        public string Category { get; set; } = string.Empty;

        [StringLength(256)]
        public string? Description { get; set; }

        public bool IsSystem { get; set; } = true;
    }
}
