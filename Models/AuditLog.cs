using System.ComponentModel.DataAnnotations;

namespace OfficeAutomation.Models
{
    public sealed class AuditLog
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [StringLength(100)]
        public string? UserId { get; set; }

        [Required]
        [StringLength(20)]
        public string Action { get; set; } = string.Empty;

        [Required]
        [StringLength(128)]
        public string TableName { get; set; } = string.Empty;

        public DateTimeOffset DateTime { get; set; } = DateTimeOffset.UtcNow;

        public string? OldValues { get; set; }

        public string? NewValues { get; set; }

        public string? AffectedColumns { get; set; }

        [StringLength(64)]
        public string? UserIP { get; set; }

        [StringLength(1024)]
        public string? UserAgent { get; set; }
    }
}
