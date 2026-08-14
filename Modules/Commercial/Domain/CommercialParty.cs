using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OfficeAutomation.Modules.Commercial.Domain;

/// <summary>
/// Unified party master data used by sales and purchase documents.
/// A party may act as a customer, a supplier, or both — avoiding duplicated
/// customer/supplier identities across the commercial module.
/// </summary>
public class CommercialParty
{
    [Key]
    public int Id { get; set; }

    [Required]
    [StringLength(150)]
    public string Name { get; set; } = string.Empty;

    public CommercialPartyType PartyType { get; set; } = CommercialPartyType.Both;

    [StringLength(12)]
    public string? NationalCode { get; set; }

    [StringLength(50)]
    public string? EconomicCode { get; set; }

    [StringLength(30)]
    public string? Phone { get; set; }

    [StringLength(150)]
    public string? Email { get; set; }

    [StringLength(400)]
    public string? Address { get; set; }

    [StringLength(400)]
    public string? Notes { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public DateTime? UpdatedAt { get; set; }

    [StringLength(450)]
    public string? CreatedByUserId { get; set; }

    public List<InvoiceReturn> Returns { get; set; } = new();
    public List<CommercialQuotation> Quotations { get; set; } = new();
}