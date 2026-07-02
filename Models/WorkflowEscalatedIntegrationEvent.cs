namespace OfficeAutomation.Models;

public sealed class WorkflowEscalatedIntegrationEvent
{
    public string EventId { get; init; } = Guid.NewGuid().ToString("N");
    public string EventType { get; init; } = "workflow.escalated";
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
    public string DocumentType { get; init; } = string.Empty;
    public int DocumentId { get; init; }
    public int WorkflowInstanceId { get; init; }
    public int WorkflowStepId { get; init; }
    public int StepNumber { get; init; }
    public string PreviousSlaState { get; init; } = WorkflowSlaState.OnTrack;
    public string NewSlaState { get; init; } = WorkflowSlaState.Breached;
    public string? EscalatedToUserId { get; init; }
    public string? EscalatedToRoleId { get; init; }
    public string? CorrelationId { get; init; }
}
