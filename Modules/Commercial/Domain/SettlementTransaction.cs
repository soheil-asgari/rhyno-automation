using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using OfficeAutomation.Models;

namespace OfficeAutomation.Modules.Commercial.Domain;

/// <summary>
/// An actual cash/bank settlement (payment or receipt) against an invoice.
/// The outstanding balance of an invoice is computed from these real settlement
/// transactions (plus approved returns), never from a stored denormalized figure.
/// </summary>
public class SettlementTransaction
{
    [Key]
    public int Id { get; set; }

    /// <summary>Direction of settlement: "Sale" (receipt from customer) or "Purchase" (payment to supplier).</summary>
    [Required]
    [StringLength(20)]
    public string SettlementType { get; set; } = "Sale";

    public int InvoiceId { get; set; }

    public Invoice? Invoice { get; set; }

    [StringLength(50)]
    public string ReferenceNumber { get; set; } = string.Empty;

    public DateTime SettlementDate { get; set; } = DateTime.Today;

    [StringLength(20)]
    public string DateShamsi { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    /// <summary>Payment method: cash, bank transfer, cheque, credit, etc.</summary>
    [StringLength(50)]
    public string? PaymentMethod { get; set; }

    [StringLength(300)]
    public string? Notes { get; set; }

    [StringLength(30)]
    public string Status { get; set; } = WorkflowStatus.Draft;

    /// <summary>Accounting voucher posted for the cash/bank movement.</summary>
    public int? VoucherHeaderId { get; set; }

    [StringLength(50)]
    public string? VoucherDocumentNumber { get; set; }

    [StringLength(450)]
    public string? CreatedByUserId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
}