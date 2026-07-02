using System.ComponentModel.DataAnnotations;

namespace OfficeAutomation.Models;

public static class WorkflowCaseTaskType
{
    public const string AdHoc = "AdHoc";
    public const string SubCase = "SubCase";
}

public static class WorkflowCaseTaskStatus
{
    public const string Pending = "Pending";
    public const string Completed = "Completed";
    public const string Canceled = "Canceled";
}

public class WorkflowCaseTask
{
    public int Id { get; set; }

    public int WorkflowInstanceId { get; set; }
    public WorkflowInstance? WorkflowInstance { get; set; }

    public int? WorkflowStepId { get; set; }
    public WorkflowStep? WorkflowStep { get; set; }

    [Required]
    [StringLength(30)]
    public string TaskType { get; set; } = WorkflowCaseTaskType.AdHoc;

    [Required]
    [StringLength(30)]
    public string Status { get; set; } = WorkflowCaseTaskStatus.Pending;

    [Required]
    [StringLength(180)]
    public string Title { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    [StringLength(450)]
    public string CreatedByUserId { get; set; } = string.Empty;
    public User? CreatedByUser { get; set; }

    [StringLength(450)]
    public string? AssignedToUserId { get; set; }
    public User? AssignedToUser { get; set; }

    public int? SubCaseInstanceId { get; set; }
    public WorkflowInstance? SubCaseInstance { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
}
