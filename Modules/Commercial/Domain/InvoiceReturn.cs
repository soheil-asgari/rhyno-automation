using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using OfficeAutomation.Models;

namespace OfficeAutomation.Modules.Commercial.Domain;

/// <summary>
/// A sales or purchase return document. Returns reference the original invoice and
/// reduce its outstanding balance; when they are finalized they post a balanced
/// reversal-style accounting voucher through the shared accounting engine.
/// </summary>
public class InvoiceReturn
{
    [Key]
    public int Id { get; set; }

    /// <summary>Direction of the return: "Sale" or "Purchase".</summary>
    [Required]
    [StringLength(20)]
    public string ReturnType { get; set; } = "Sale";

    [Required]
    [StringLength(50)]
    public string ReturnNumber { get; set; } = string.Empty;

    /// <summary>The original invoice this return reduces.</summary>
    public int InvoiceId { get; set; }

    public Invoice? Invoice { get; set; }

    /// <summary>Optional unified party reference (mirrors the source invoice party).</summary>
    public int? CommercialPartyId { get; set; }

    public CommercialParty? CommercialParty { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal SubTotal { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal VatAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal GrandTotal { get; set; }

    [StringLength(300)]
    public string? Reason { get; set; }

    [StringLength(30)]
    public string Status { get; set; } = WorkflowStatus.Draft;

    /// <summary>Reference to the accounting voucher posted when the return is finalized.</summary>
    public int? VoucherHeaderId { get; set; }

    [StringLength(50)]
    public string? VoucherDocumentNumber { get; set; }

    public DateTime ReturnDate { get; set; } = DateTime.Today;

    [StringLength(20)]
    public string DateShamsi { get; set; } = string.Empty;

    [StringLength(450)]
    public string? CreatedByUserId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public List<InvoiceReturnItem> Items { get; set; } = new();
}