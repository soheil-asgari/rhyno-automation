using System.ComponentModel.DataAnnotations;

namespace OfficeAutomation.Modules.Persistence.Entities;

/// <summary>
/// Seller (فروشنده) entity — party issuing the invoice.
/// </summary>
public class Seller
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(50)]
    public string? EconomicCode { get; set; }

    [StringLength(20)]
    public string? NationalId { get; set; }

    [StringLength(20)]
    public string? Phone { get; set; }

    [StringLength(400)]
    public string? Address { get; set; }

    [StringLength(200)]
    public string? RegistrationNumber { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public List<InvoiceRecord> Invoices { get; set; } = new();
}
