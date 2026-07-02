using System.ComponentModel.DataAnnotations;

namespace OfficeAutomation.Models;

public class WorkflowAttachment
{
    public int Id { get; set; }
    public int WorkflowInstanceId { get; set; }
    public WorkflowInstance? WorkflowInstance { get; set; }
    public int? WorkflowStepId { get; set; }
    public WorkflowStep? WorkflowStep { get; set; }
    public int? WorkflowDecisionId { get; set; }
    public WorkflowDecision? WorkflowDecision { get; set; }

    [Required]
    [StringLength(260)]
    public string FileName { get; set; } = string.Empty;

    [Required]
    [StringLength(400)]
    public string FilePath { get; set; } = string.Empty;

    [StringLength(120)]
    public string ContentType { get; set; } = string.Empty;

    public long FileSize { get; set; }

    [Required]
    [StringLength(450)]
    public string UploadedByUserId { get; set; } = string.Empty;
    public User? UploadedByUser { get; set; }

    public DateTimeOffset UploadedAt { get; set; } = DateTimeOffset.UtcNow;
}
