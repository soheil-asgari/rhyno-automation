using System.ComponentModel.DataAnnotations;

namespace OfficeAutomation.Models;

public class WorkflowTransitionEvent
{
    public long Id { get; set; }

    public int WorkflowInstanceId { get; set; }
    public WorkflowInstance? WorkflowInstance { get; set; }

    public int? WorkflowStepId { get; set; }
    public WorkflowStep? WorkflowStep { get; set; }

    public long SequenceNumber { get; set; }

    [Required]
    [StringLength(64)]
    public string EventName { get; set; } = string.Empty;

    [StringLength(30)]
    public string? FromStatus { get; set; }

    [StringLength(30)]
    public string? ToStatus { get; set; }

    public int? FromStepNumber { get; set; }
    public int? ToStepNumber { get; set; }

    [StringLength(450)]
    public string? ActorUserId { get; set; }
    public User? ActorUser { get; set; }

    [StringLength(80)]
    public string? StationKey { get; set; }

    [StringLength(80)]
    public string? StationName { get; set; }

    [StringLength(80)]
    public string? CorrelationKey { get; set; }

    [StringLength(2000)]
    public string? PayloadJson { get; set; }

    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
}
