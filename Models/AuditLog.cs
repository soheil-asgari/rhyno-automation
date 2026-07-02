using System.ComponentModel.DataAnnotations;

namespace OfficeAutomation.Models
{
    public sealed class AuditLog
    {
        public Guid Id { get; init; } = Guid.NewGuid();

        [StringLength(64)]
        public string? TenantId { get; init; }

        [StringLength(100)]
        public string? UserId { get; init; }

        [Required]
        [StringLength(20)]
        public string Action { get; init; } = string.Empty;

        [Required]
        [StringLength(128)]
        public string TableName { get; init; } = string.Empty;

        [StringLength(128)]
        public string? EntityId { get; init; }

        [StringLength(128)]
        public string? CorrelationId { get; init; }

        public DateTimeOffset DateTime { get; init; } = DateTimeOffset.UtcNow;

        public string? OldValues { get; init; }

        public string? NewValues { get; init; }

        public string? AffectedColumns { get; init; }

        [StringLength(64)]
        public string? UserIP { get; init; }

        [StringLength(1024)]
        public string? UserAgent { get; init; }

        public bool IsSensitive { get; init; }

        public string? UserContext { get; init; }

        public string? ChangeSet { get; init; }

        [StringLength(32)]
        public string? Severity { get; init; }

        [StringLength(64)]
        public string? ComplianceCategory { get; init; }

        public string? StructuredPayload { get; init; }

        [StringLength(128)]
        public string? IntegrityHash { get; init; }
    }
}
