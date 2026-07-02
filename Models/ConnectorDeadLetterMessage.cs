using System.ComponentModel.DataAnnotations;

namespace OfficeAutomation.Models;

public static class ConnectorDeadLetterStatus
{
    public const string Pending = "Pending";
    public const string Replayed = "Replayed";
    public const string Ignored = "Ignored";
}

public class ConnectorDeadLetterMessage
{
    public long Id { get; set; }

    [Required]
    [StringLength(80)]
    public string ConnectorName { get; set; } = string.Empty;

    [Required]
    [StringLength(120)]
    public string OperationName { get; set; } = string.Empty;

    [StringLength(128)]
    public string? CorrelationId { get; set; }

    [Required]
    public string PayloadJson { get; set; } = string.Empty;

    [Required]
    [StringLength(32)]
    public string Status { get; set; } = ConnectorDeadLetterStatus.Pending;

    [StringLength(1200)]
    public string? ErrorMessage { get; set; }

    public int AttemptCount { get; set; }
    public DateTimeOffset FailedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastRetriedAt { get; set; }
}
