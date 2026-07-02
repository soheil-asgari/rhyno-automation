namespace OfficeAutomation.Models;

public sealed class WorkflowStatusChangedIntegrationEvent
{
    public string EventId { get; init; } = Guid.NewGuid().ToString("N");
    public string EventType { get; init; } = "workflow.status.changed";
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
    public string DocumentType { get; init; } = string.Empty;
    public int DocumentId { get; init; }
    public int WorkflowInstanceId { get; init; }
    public int? WorkflowStepId { get; init; }
    public int StepNumber { get; init; }
    public string DecisionType { get; init; } = string.Empty;
    public string PreviousStatus { get; init; } = string.Empty;
    public string CurrentStatus { get; init; } = string.Empty;
    public string CurrentWorkflowStatus { get; init; } = string.Empty;
    public string? CurrentAssigneeUserId { get; init; }
    public string? CurrentAssigneeRoleId { get; init; }
    public int? CurrentAssigneeDepartmentId { get; init; }
    public string ActorUserId { get; init; } = string.Empty;
    public string? Comment { get; init; }
    public string? CorrelationId { get; init; }
}
