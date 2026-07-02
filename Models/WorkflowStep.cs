using System.ComponentModel.DataAnnotations;

namespace OfficeAutomation.Models;

public class WorkflowStep
{
    public int Id { get; set; }

    public int WorkflowInstanceId { get; set; }

    public WorkflowInstance? WorkflowInstance { get; set; }

    [Range(1, 100)]
    public int StepNumber { get; set; }
    [StringLength(80)]
    public string? StepKey { get; set; }
    [StringLength(80)]
    public string? StepName { get; set; }
    [StringLength(30)]
    public string AssignmentMode { get; set; } = WorkflowAssignmentMode.User;

    [StringLength(450)]
    public string? AssignedToUserId { get; set; }

    public User? AssignedToUser { get; set; }
    [StringLength(450)]
    public string? AssignedRoleId { get; set; }
    public ApplicationRole? AssignedRole { get; set; }
    public int? AssignedDepartmentId { get; set; }
    public Department? AssignedDepartment { get; set; }
    [StringLength(450)]
    public string? DelegatedFromUserId { get; set; }
    public User? DelegatedFromUser { get; set; }

    [Required]
    [StringLength(30)]
    public string Status { get; set; } = WorkflowStatus.PendingApproval;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? DueAt { get; set; }
    public DateTimeOffset? ReadAt { get; set; }
    public DateTimeOffset? EscalatedAt { get; set; }
    [StringLength(30)]
    public string? SlaState { get; set; }
    public int? ReturnedFromStepNumber { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }
    public List<WorkflowSlaJob> SlaJobs { get; set; } = new();
    public List<WorkflowCaseTask> CaseTasks { get; set; } = new();
    public List<WorkflowTransitionEvent> TransitionEvents { get; set; } = new();
}
