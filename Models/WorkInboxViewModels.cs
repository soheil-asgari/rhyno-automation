namespace OfficeAutomation.Models;

public sealed class WorkInboxVM
{
    public string ActiveFilter { get; init; } = "All";
    public int TotalCount { get; init; }
    public int ApprovalCount { get; init; }
    public int LetterCount { get; init; }
    public int AlertCount { get; init; }
    public int OverdueCount { get; init; }
    public int DueSoonCount { get; init; }
    public int UnreadCount { get; init; }
    public string? CurrentUserId { get; init; }
    public IReadOnlyList<WorkInboxItemVM> Items { get; init; } = [];
    public IReadOnlyList<WorkInboxAssigneeVM> Assignees { get; init; } = [];
}

public sealed class WorkInboxItemVM
{
    public string Id { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string Module { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string StatusCssClass { get; init; } = "text-bg-secondary";
    public string Priority { get; init; } = "Normal";
    public string PriorityCssClass { get; init; } = "inbox-priority-normal";
    public string GroupLabel { get; init; } = string.Empty;
    public string CreatedAtText { get; init; } = string.Empty;
    public DateTimeOffset SortDate { get; init; }
    public string Url { get; init; } = "#";
    public string Icon { get; init; } = "bi-circle";
    public bool RequiresAction { get; init; }
    public bool IsRead { get; init; }
    public string? SenderName { get; init; }
    public string SlaState { get; init; } = WorkflowSlaState.OnTrack;
    public string SlaText { get; init; } = string.Empty;
    public DateTimeOffset? Deadline { get; init; }
    public bool IsOverdue { get; init; }
    public bool IsExpired { get; init; }
    public string SearchText { get; init; } = string.Empty;
    public string DetailTitle { get; init; } = string.Empty;
    public string DetailSummary { get; init; } = string.Empty;
    public string? DetailCaption { get; init; }
    public string? DocumentType { get; init; }
    public int? DocumentId { get; init; }
    public int? StepNumber { get; init; }
    public int? WorkflowStepId { get; init; }
    public bool CanInlineApprove { get; init; }
    public bool CanInlineReject { get; init; }
    public bool CanInlineComment { get; init; }
    public bool CanInlineDelegate { get; init; }
}

public sealed class WorkInboxAssigneeVM
{
    public string UserId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
}

public sealed class WorkInboxBulkActionVM
{
    public string? Filter { get; init; }
    public string Action { get; init; } = string.Empty;
    public List<string> SelectedIds { get; init; } = [];
    public string? Note { get; init; }
    public string? ToUserId { get; init; }
}
