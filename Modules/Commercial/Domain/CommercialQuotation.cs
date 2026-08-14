using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using OfficeAutomation.Models;

namespace OfficeAutomation.Modules.Commercial.Domain;

/// <summary>
/// A sales or purchase quotation. A quotation can be converted into an invoice:
/// Quotation → Invoice. Customers and product data are reused — never duplicated.
/// </summary>
public class CommercialQuotation
{
    [Key]
    public int Id { get; set; }

    /// <summary>Direction: "Sale" or "Purchase".</summary>
    [Required]
    [StringLength(20)]
    public string QuotationType { get; set; } = "Sale";

    [Required]
    [StringLength(50)]
    public string QuotationNumber { get; set; } = string.Empty;

    public int? CommercialPartyId { get; set; }

    public CommercialParty? CommercialParty { get; set; }

    [StringLength(150)]
    public string PartyName { get; set; } = string.Empty;

    [StringLength(30)]
    public string? NationalCodeOrEconomicId { get; set; }

    public DateTime QuotationDate { get; set; } = DateTime.Today;

    [StringLength(20)]
    public string DateShamsi { get; set; } = string.Empty;

    public DateTime? ValidUntil { get; set; }

    [StringLength(20)]
    public string? ValidUntilShamsi { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal SubTotal { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal VatAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal GrandTotal { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal DiscountAmount { get; set; }

    [StringLength(400)]
    public string? Notes { get; set; }

    [StringLength(30)]
    public string Status { get; set; } = CommercialQuotationStatus.Draft;

    /// <summary>Invoice produced by converting this quotation.</summary>
    public int? ConvertedInvoiceId { get; set; }

    [StringLength(450)]
    public string? CreatedByUserId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public List<CommercialQuotationLine> Lines { get; set; } = new();

    [NotMapped]
    public bool IsExpired => ValidUntil.HasValue && ValidUntil.Value.Date < DateTime.Today && Status == CommercialQuotationStatus.Sent;
}