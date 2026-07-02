using System.ComponentModel.DataAnnotations;

namespace OfficeAutomation.Models;

public class WorkflowIncident
{
    public int Id { get; set; }

    public int WorkflowInstanceId { get; set; }
    public WorkflowInstance? WorkflowInstance { get; set; }

    public int? WorkflowStepId { get; set; }
    public WorkflowStep? WorkflowStep { get; set; }

    [Required]
    [StringLength(80)]
    public string IncidentType { get; set; } = "System";

    [Required]
    [StringLength(120)]
    public string ErrorCode { get; set; } = "WorkflowIncident";

    [Required]
    [StringLength(2000)]
    public string ErrorMessage { get; set; } = string.Empty;

    public string? ErrorDetails { get; set; }

    [StringLength(450)]
    public string? ActorUserId { get; set; }
    public User? ActorUser { get; set; }

    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ResolvedAt { get; set; }
    public DateTimeOffset? RetriedAt { get; set; }

    [StringLength(450)]
    public string? ResolvedByUserId { get; set; }

    [StringLength(1000)]
    public string? ResolutionNote { get; set; }

    public bool IsResolved { get; set; }
}
