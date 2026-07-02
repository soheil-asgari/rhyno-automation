using System.ComponentModel.DataAnnotations;

namespace OfficeAutomation.Models;

public class WorkflowActionLog
{
    public int Id { get; set; }
    public int WorkflowInstanceId { get; set; }
    public WorkflowInstance? WorkflowInstance { get; set; }
    public int? WorkflowStepId { get; set; }
    public WorkflowStep? WorkflowStep { get; set; }

    [Required]
    [StringLength(450)]
    public string ActorUserId { get; set; } = string.Empty;
    public User? ActorUser { get; set; }

    [Required]
    [StringLength(40)]
    public string ActionType { get; set; } = WorkflowDecisionType.Comment;

    [StringLength(1000)]
    public string? Description { get; set; }

    [StringLength(2000)]
    public string? MetadataJson { get; set; }

    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
}
