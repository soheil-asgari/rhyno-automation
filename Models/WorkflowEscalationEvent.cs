using System.ComponentModel.DataAnnotations;

namespace OfficeAutomation.Models;

public class WorkflowEscalationEvent
{
    public int Id { get; set; }
    public int WorkflowInstanceId { get; set; }
    public WorkflowInstance? WorkflowInstance { get; set; }
    public int? WorkflowStepId { get; set; }
    public WorkflowStep? WorkflowStep { get; set; }

    [StringLength(450)]
    public string? EscalatedToUserId { get; set; }
    public User? EscalatedToUser { get; set; }

    [StringLength(450)]
    public string? EscalatedToRoleId { get; set; }
    public ApplicationRole? EscalatedToRole { get; set; }

    [Required]
    [StringLength(30)]
    public string PreviousSlaState { get; set; } = WorkflowSlaState.OnTrack;

    [Required]
    [StringLength(30)]
    public string NewSlaState { get; set; } = WorkflowSlaState.Breached;

    [StringLength(1000)]
    public string? Note { get; set; }

    public DateTimeOffset EscalatedAt { get; set; } = DateTimeOffset.UtcNow;
}
