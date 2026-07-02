namespace OfficeAutomation.Models;

public sealed class WorkflowDetailPanelVM
{
    public string DocumentType { get; init; } = string.Empty;
    public int DocumentId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public string CurrentStatus { get; init; } = WorkflowStatus.Draft;
    public string? CurrentAssigneeName { get; init; }
    public string? CurrentAssigneeId { get; init; }
    public string SlaState { get; init; } = WorkflowSlaState.OnTrack;
    public DateTimeOffset? Deadline { get; init; }
    public int CurrentStepNumber { get; init; }
    public int? CurrentWorkflowStepId { get; init; }
    public bool CanApprove { get; init; }
    public bool CanReject { get; init; }
    public bool CanReturn { get; init; }
    public bool CanRequestChanges { get; init; }
    public bool CanForward { get; init; }
    public bool CanDelegate { get; init; }
    public IReadOnlyList<WorkflowActionLog> Timeline { get; init; } = [];
    public IReadOnlyList<WorkflowDecision> Decisions { get; init; } = [];
    public IReadOnlyList<WorkflowComment> Comments { get; init; } = [];
    public IReadOnlyList<WorkflowAttachment> Attachments { get; init; } = [];
    public IReadOnlyList<WorkflowUserOptionVM> UserOptions { get; init; } = [];
}

public sealed class WorkflowUserOptionVM
{
    public string Id { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
}
