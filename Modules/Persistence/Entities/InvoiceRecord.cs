using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OfficeAutomation.Modules.Persistence.Entities;

/// <summary>
/// Persisted invoice record from the intake pipeline.
/// Mirrors ExtractedInvoice but as a database entity.
/// </summary>
public class InvoiceRecord
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, StringLength(100)]
    public string InvoiceNumber { get; set; } = string.Empty;

    [Required, StringLength(20)]
    public string DateShamsi { get; set; } = string.Empty;

    public DateTime? InvoiceDate { get; set; }

    /// <summary>All amounts stored in Rial.</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal SubTotal { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal DiscountAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal VatAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal DutyAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal FinalAmount { get; set; }

    [Column(TypeName = "decimal(5,4)")]
    public decimal VatRate { get; set; } = 0.10m;

    public Guid? SellerId { get; set; }
    public Seller? Seller { get; set; }

    public Guid? BuyerId { get; set; }
    public Buyer? Buyer { get; set; }

    [StringLength(50)]
    public string InvoiceType { get; set; } = "Sale";

    [StringLength(50)]
    public string Status { get; set; } = "Processed";

    /// <summary>Raw OCR text for full-text search.</summary>
    public string? RawOcrText { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }

    [StringLength(50)]
    public string? OwnerKey { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// Indicates the record was imported/synchronized from a Mowadiyan (سامانه مودیان)
    /// output-file export rather than the local OCR intake pipeline.
    /// </summary>
    public bool IsMowadiyanSynced { get; set; }

    public List<InvoiceLineItem> LineItems { get; set; } = new();
    public List<TaxLog> TaxLogs { get; set; } = new();
    public List<MowadiyanSubmission> MowadiyanSubmissions { get; set; } = new();
    public List<InvoiceReconciliationResult> ReconciliationResults { get; set; } = new();
}
