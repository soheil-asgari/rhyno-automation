using System.ComponentModel.DataAnnotations;

namespace OfficeAutomation.Models;

public sealed class OutboxMessage
{
    public long Id { get; set; }

    [Required]
    [StringLength(80)]
    public string MessageId { get; set; } = Guid.NewGuid().ToString("N");

    [StringLength(64)]
    public string? TenantId { get; set; }

    [Required]
    [StringLength(120)]
    public string EventType { get; set; } = string.Empty;

    [Required]
    [StringLength(120)]
    public string AggregateType { get; set; } = string.Empty;

    [Required]
    [StringLength(128)]
    public string AggregateId { get; set; } = string.Empty;

    [Required]
    [StringLength(120)]
    public string ExchangeName { get; set; } = string.Empty;

    [Required]
    [StringLength(160)]
    public string RoutingKey { get; set; } = string.Empty;

    [Required]
    public string PayloadJson { get; set; } = string.Empty;

    [Required]
    [StringLength(32)]
    public string Status { get; set; } = OutboxMessageStatus.Pending;

    [StringLength(128)]
    public string? CorrelationId { get; set; }

    public int RetryCount { get; set; }

    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? LockedUntil { get; set; }

    public DateTimeOffset? ProcessedAt { get; set; }

    public DateTimeOffset? LastAttemptAt { get; set; }

    [StringLength(1200)]
    public string? LastError { get; set; }
}
