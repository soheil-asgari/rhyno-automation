using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OfficeAutomation.Modules.Persistence.Entities;

/// <summary>Overall reconciliation outcome for an invoice.</summary>
public enum ReconciliationStatus
{
    Pending = 0,
    Matched = 1,
    Mismatched = 2,
    NotSubmitted = 3
}

/// <summary>
/// A single detected discrepancy between a local invoice record and the data
/// returned by the Mowadiyan (Tax Portal) API, together with a suggested fix.
/// </summary>
public class ReconciliationDiscrepancy
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ReconciliationResultId { get; set; }
    public InvoiceReconciliationResult ReconciliationResult { get; set; } = null!;

    /// <summary>Discrepancy type code (e.g. AmountMismatch, VatRateMismatch, ...).</summary>
    [StringLength(60)]
    public string Type { get; set; } = string.Empty;

    /// <summary>Local value (from InvoiceRecord).</summary>
    [StringLength(500)]
    public string? LocalValue { get; set; }

    /// <summary>Portal value (from Mowadiyan response).</summary>
    [StringLength(500)]
    public string? PortalValue { get; set; }

    /// <summary>Human-readable description in Persian.</summary>
    [StringLength(1000)]
    public string? Description { get; set; }

    /// <summary>Suggested resolution in Persian.</summary>
    [StringLength(1000)]
    public string? Suggestion { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

/// <summary>
/// Side-by-side reconciliation result between a local <see cref="InvoiceRecord"/>
/// and the corresponding Mowadiyan (Tax Portal) submission response.
/// </summary>
public class InvoiceReconciliationResult
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid InvoiceRecordId { get; set; }
    public InvoiceRecord InvoiceRecord { get; set; } = null!;

    public Guid? SubmissionId { get; set; }
    public MowadiyanSubmission? Submission { get; set; }

    public ReconciliationStatus Status { get; set; } = ReconciliationStatus.Pending;

    /// <summary>Tax Reference ID (شماره پیگیری مالیاتی) from the portal.</summary>
    [StringLength(100)]
    public string? TaxReferenceId { get; set; }

    /// <summary>Amount (ریال) from the portal response.</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal? PortalTotalAmount { get; set; }

    /// <summary>VAT (مالیات) amount (ریال) from the portal response.</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal? PortalVatAmount { get; set; }

    /// <summary>VAT rate applied by the portal.</summary>
    [Column(TypeName = "decimal(5,4)")]
    public decimal? PortalVatRate { get; set; }

    /// <summary>Economic code of the seller as recorded by the portal.</summary>
    [StringLength(50)]
    public string? PortalSellerEconomicCode { get; set; }

    /// <summary>Economic code of the buyer as recorded by the portal.</summary>
    [StringLength(50)]
    public string? PortalBuyerEconomicCode { get; set; }

    /// <summary>Number of discrepancies detected.</summary>
    public int DiscrepancyCount { get; set; }

    public List<ReconciliationDiscrepancy> Discrepancies { get; set; } = new();

    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}