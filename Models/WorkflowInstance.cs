using System.ComponentModel.DataAnnotations;

namespace OfficeAutomation.Models;

public class WorkflowInstance
{
    public int Id { get; set; }

    public int? DefinitionVersionId { get; set; }

    public WorkflowDefinitionVersion? DefinitionVersion { get; set; }

    [Required]
    [StringLength(60)]
    public string DocumentType { get; set; } = string.Empty;

    public int DocumentId { get; set; }

    [Required]
    [StringLength(30)]
    public string Status { get; set; } = WorkflowStatus.PendingApproval;
    [StringLength(30)]
    public string CurrentStatus { get; set; } = WorkflowStatus.PendingApproval;

    public int CurrentStepNumber { get; set; }
    [StringLength(30)]
    public string Priority { get; set; } = "Normal";

    [StringLength(450)]
    public string? StartedByUserId { get; set; }

    public User? StartedByUser { get; set; }

    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? CompletedAt { get; set; }

    public DateTimeOffset? DueAt { get; set; }

    [StringLength(30)]
    public string? SlaState { get; set; }
    public DateTimeOffset? LastActionAt { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
    [StringLength(450)]
    public string? CurrentAssigneeUserId { get; set; }
    public User? CurrentAssigneeUser { get; set; }
    [StringLength(450)]
    public string? CurrentAssigneeRoleId { get; set; }
    public ApplicationRole? CurrentAssigneeRole { get; set; }
    public int? CurrentAssigneeDepartmentId { get; set; }
    public Department? CurrentAssigneeDepartment { get; set; }

    public List<WorkflowStep> Steps { get; set; } = new();

    public List<WorkflowDecision> Decisions { get; set; } = new();
    public List<WorkflowActionLog> ActionLogs { get; set; } = new();
    public List<WorkflowAttachment> Attachments { get; set; } = new();
    public List<WorkflowComment> Comments { get; set; } = new();
    public List<WorkflowEscalationEvent> Escalations { get; set; } = new();
    public List<WorkflowSlaJob> SlaJobs { get; set; } = new();
    public int? ParentWorkflowInstanceId { get; set; }
    public WorkflowInstance? ParentWorkflowInstance { get; set; }
    public List<WorkflowInstance> SubCases { get; set; } = new();
    public List<WorkflowCaseTask> CaseTasks { get; set; } = new();
    public List<WorkflowTransitionEvent> TransitionEvents { get; set; } = new();
    public List<WorkflowIncident> Incidents { get; set; } = new();
    public List<DocumentSignature> DocumentSignatures { get; set; } = new();
}
