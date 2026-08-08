using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OfficeAutomation.Modules.Persistence.Entities;

/// <summary>
/// A single line item within a persisted invoice record.
/// </summary>
public class InvoiceLineItem
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid InvoiceRecordId { get; set; }
    public InvoiceRecord InvoiceRecord { get; set; } = null!;

    [Required, StringLength(300)]
    public string Description { get; set; } = string.Empty;

    [StringLength(50)]
    public string Kind { get; set; } = "Item"; // Item, Discount, Tax, Duty, Shipping, Insurance

    [Column(TypeName = "decimal(18,3)")]
    public decimal Quantity { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal UnitPrice { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal LineAmount { get; set; }

    public bool IsTaxExempt { get; set; }

    /// <summary>Original raw text from OCR.</summary>
    [StringLength(500)]
    public string? RawText { get; set; }

    public int DisplayOrder { get; set; }
}
