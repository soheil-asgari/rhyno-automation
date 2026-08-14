using System.ComponentModel.DataAnnotations;

namespace OfficeAutomation.Modules.Commercial.Domain;

/// <summary>
/// A financial attachment linked to a commercial invoice (scan, contract, receipt,
/// or supporting document). The binary content lives on disk; this entity holds the
/// safe metadata that can be listed and downloaded.
/// </summary>
public class InvoiceAttachment
{
    [Key]
    public int Id { get; set; }

    public int InvoiceId { get; set; }

    public OfficeAutomation.Models.Invoice? Invoice { get; set; }

    public InvoiceAttachmentCategory Category { get; set; } = InvoiceAttachmentCategory.SupportingDocument;

    [Required]
    [StringLength(255)]
    public string FileName { get; set; } = string.Empty;

    [Required]
    [StringLength(512)]
    public string StoredPath { get; set; } = string.Empty;

    [StringLength(100)]
    public string ContentType { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    [StringLength(450)]
    public string? UploadedByUserId { get; set; }

    public DateTime UploadedAt { get; set; } = DateTime.Now;

    [StringLength(300)]
    public string? Description { get; set; }
}