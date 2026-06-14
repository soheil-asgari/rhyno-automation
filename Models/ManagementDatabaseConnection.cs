using System.ComponentModel.DataAnnotations;

namespace OfficeAutomation.Models
{
    public sealed class ManagementDatabaseConnection
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [StringLength(40)]
        public string Provider { get; set; } = "SqlServer";

        [Required]
        [StringLength(256)]
        public string Host { get; set; } = string.Empty;

        public int? Port { get; set; }

        [StringLength(128)]
        public string? DatabaseName { get; set; }

        [StringLength(128)]
        public string? Username { get; set; }

        public string? ProtectedPassword { get; set; }

        public bool TrustServerCertificate { get; set; } = true;

        [StringLength(100)]
        public string? CreatedByUserId { get; set; }

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
