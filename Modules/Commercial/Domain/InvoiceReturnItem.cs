using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OfficeAutomation.Modules.Commercial.Domain;

public class InvoiceReturnItem
{
    public int Id { get; set; }

    public int InvoiceReturnId { get; set; }

    public InvoiceReturn InvoiceReturn { get; set; } = null!;

    public int? ProductId { get; set; }

    [Required]
    [StringLength(150)]
    public string ItemName { get; set; } = string.Empty;

    [Range(0.001, 99999999999)]
    [Column(TypeName = "decimal(18,3)")]
    public decimal Quantity { get; set; }

    [Range(0, 99999999999)]
    [Column(TypeName = "decimal(18,2)")]
    public decimal UnitPrice { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal LineSubTotal { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal LineVatAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal LineGrandTotal { get; set; }
}