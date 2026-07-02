using System.ComponentModel.DataAnnotations;

namespace OfficeAutomation.Models;

public static class WorkflowSlaJobStatus
{
    public const string Scheduled = "Scheduled";
    public const string Completed = "Completed";
    public const string Canceled = "Canceled";
}

public class WorkflowSlaJob
{
    public int Id { get; set; }

    public int WorkflowInstanceId { get; set; }
    public WorkflowInstance? WorkflowInstance { get; set; }

    public int WorkflowStepId { get; set; }
    public WorkflowStep? WorkflowStep { get; set; }

    [Required]
    [StringLength(32)]
    public string Status { get; set; } = WorkflowSlaJobStatus.Scheduled;

    public DateTimeOffset ScheduledFor { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset? CanceledAt { get; set; }

    [StringLength(200)]
    public string? CancellationReason { get; set; }
}
