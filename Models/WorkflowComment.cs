using System.ComponentModel.DataAnnotations;

namespace OfficeAutomation.Models;

public class WorkflowComment
{
    public int Id { get; set; }
    public int WorkflowInstanceId { get; set; }
    public WorkflowInstance? WorkflowInstance { get; set; }
    public int? WorkflowStepId { get; set; }
    public WorkflowStep? WorkflowStep { get; set; }

    [Required]
    [StringLength(450)]
    public string AuthorUserId { get; set; } = string.Empty;
    public User? AuthorUser { get; set; }

    [Required]
    [StringLength(1000)]
    public string Body { get; set; } = string.Empty;

    [StringLength(30)]
    public string Visibility { get; set; } = "Participants";

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
