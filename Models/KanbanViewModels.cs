namespace OfficeAutomation.Models;

/// <summary>
/// نمای کانبان برای تسک‌های کارتابل
/// </summary>
public sealed class KanbanBoardVM
{
    public IReadOnlyList<KanbanColumnVM> Columns { get; init; } = [];
}

public sealed class KanbanColumnVM
{
    public string Status { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string ColorClass { get; init; } = string.Empty;
    public IReadOnlyList<KanbanCardVM> Cards { get; init; } = [];
}

public sealed class KanbanCardVM
{
    public int TaskId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? AssignedToName { get; init; }
    public string[] Labels { get; init; } = [];
    public int CommentCount { get; init; }
    public decimal TotalHoursSpent { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}

/// <summary>
/// مدل ثبت زمان کارکرد روی تسک
/// </summary>
public sealed class TaskTimeLogVM
{
    public int WorkflowCaseTaskId { get; set; }

    [System.ComponentModel.DataAnnotations.Range(0.01, 24)]
    public decimal HoursSpent { get; set; }

    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.StringLength(1000)]
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// مدل نمایش زمان‌های ثبت‌شده
/// </summary>
public sealed class TaskTimeLogItemVM
{
    public int Id { get; init; }
    public string UserName { get; init; } = string.Empty;
    public decimal HoursSpent { get; init; }
    public string Description { get; init; } = string.Empty;
    public DateTime LoggedAt { get; init; }
}

/// <summary>
/// ثبت کامنت با پشتیبانی از @mention
/// </summary>
public sealed class AddCommentWithMentionsVM
{
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.StringLength(1000)]
    public string Body { get; set; } = string.Empty;

    public string? DocumentType { get; set; }
    public int? DocumentId { get; set; }
    public int? WorkflowStepId { get; set; }
}