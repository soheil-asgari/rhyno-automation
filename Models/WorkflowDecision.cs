using System.ComponentModel.DataAnnotations;

namespace OfficeAutomation.Models;

public class WorkflowDecision
{
    public int Id { get; set; }

    public int WorkflowInstanceId { get; set; }

    public WorkflowInstance? WorkflowInstance { get; set; }

    public int? WorkflowStepId { get; set; }

    public WorkflowStep? WorkflowStep { get; set; }

    [Required]
    [StringLength(450)]
    public string DecidedByUserId { get; set; } = string.Empty;

    public User? DecidedByUser { get; set; }

    [Required]
    [StringLength(30)]
    public string Decision { get; set; } = string.Empty;
    [Required]
    [StringLength(40)]
    public string DecisionType { get; set; } = WorkflowDecisionType.Comment;

    [StringLength(1000)]
    public string? Comment { get; set; }
    [StringLength(500)]
    [Obsolete("Use DocumentSignatures for immutable cryptographic workflow signatures.")]
    public string? SignatureText { get; set; }
    public int AttachmentCount { get; set; }

    public DateTimeOffset DecidedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<DocumentSignature> DocumentSignatures { get; set; } = new();
}
