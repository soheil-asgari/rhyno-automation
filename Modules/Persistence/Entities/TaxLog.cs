using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OfficeAutomation.Modules.Persistence.Entities;

/// <summary>
/// Audit log of tax calculations applied to an invoice.
/// </summary>
public class TaxLog
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid InvoiceRecordId { get; set; }
    public InvoiceRecord InvoiceRecord { get; set; } = null!;

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalBeforeTax { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalDiscount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TaxableAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal ExemptAmount { get; set; }

    [Column(TypeName = "decimal(5,4)")]
    public decimal VatRate { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TaxAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal DutyAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal FinalAmount { get; set; }

    public bool HasExemptItems { get; set; }
    public bool IsFullyExempt { get; set; }

    [StringLength(1000)]
    public string? SummaryPersian { get; set; }

    [StringLength(50)]
    public string? CalculatedBy { get; set; }

    public DateTime CalculatedAt { get; set; } = DateTime.Now;
}
