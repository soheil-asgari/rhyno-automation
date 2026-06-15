using System;
using System.ComponentModel.DataAnnotations;

namespace OfficeAutomation.Models
{
    public class UserPreference
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(450)]
        public string UserId { get; set; } = string.Empty;

        public bool SidebarCollapsedByDefault { get; set; }

        [Required]
        [MaxLength(16)]
        public string ThemePreference { get; set; } = "System";

        public string? TablePreferencesJson { get; set; }

        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

        public User? User { get; set; }
    }
}
