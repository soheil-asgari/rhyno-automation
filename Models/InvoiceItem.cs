using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OfficeAutomation.Models
{
    public class InvoiceItem
    {
        public int Id { get; set; }

        public int InvoiceId { get; set; }

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

        public Invoice Invoice { get; set; } = null!;

        public Product? Product { get; set; }
    }
}
