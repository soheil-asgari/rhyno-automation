using System.ComponentModel.DataAnnotations;

namespace OfficeAutomation.Models;

public class DocumentSignature
{
    public int Id { get; set; }

    public int WorkflowInstanceId { get; set; }
    public WorkflowInstance? WorkflowInstance { get; set; }

    public int WorkflowDecisionId { get; set; }
    public WorkflowDecision? WorkflowDecision { get; set; }

    [Required]
    [StringLength(60)]
    public string DocumentType { get; set; } = string.Empty;

    public int DocumentId { get; set; }

    [Required]
    [StringLength(450)]
    public string SignerUserId { get; set; } = string.Empty;
    public User? SignerUser { get; set; }

    [Required]
    [StringLength(128)]
    public string PayloadHash { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string HashAlgorithm { get; set; } = "SHA256";

    [Required]
    [StringLength(100)]
    public string SignatureKeyId { get; set; } = string.Empty;

    [Required]
    public string SignatureValue { get; set; } = string.Empty;

    [StringLength(128)]
    public string? CertificateThumbprint { get; set; }

    [StringLength(500)]
    public string? CertificateSubject { get; set; }

    public DateTimeOffset SignedAt { get; set; } = DateTimeOffset.UtcNow;

    [Required]
    public string CanonicalPayload { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? SignerDisplayName { get; set; }

    [StringLength(1000)]
    public string? SigningReason { get; set; }
}
