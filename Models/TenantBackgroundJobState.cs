using System.ComponentModel.DataAnnotations;

namespace OfficeAutomation.Models;

public sealed class TenantBackgroundJobState
{
    public int Id { get; set; }

    [Required]
    [StringLength(64)]
    public string TenantId { get; set; } = string.Empty;

    [Required]
    [StringLength(128)]
    public string JobName { get; set; } = string.Empty;

    [Required]
    [StringLength(128)]
    public string JobNamespace { get; set; } = string.Empty;

    public DateTimeOffset LockedUntil { get; set; }

    [StringLength(128)]
    public string? LockedBy { get; set; }

    public DateTimeOffset? LastStartedAt { get; set; }
    public DateTimeOffset? LastCompletedAt { get; set; }
    public DateTimeOffset? LastFailedAt { get; set; }

    [StringLength(1200)]
    public string? LastError { get; set; }
}
