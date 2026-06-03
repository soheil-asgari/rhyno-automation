using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OfficeAutomation.Models
{
    public class Invoice
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "شماره فاکتور الزامی است.")]
        [StringLength(50)]
        public string InvoiceNumber { get; set; } = string.Empty;

        [StringLength(20)]
        public string InvoiceType { get; set; } = "Sale";

        [StringLength(20)]
        public string DateShamsi { get; set; } = string.Empty;

        [StringLength(150)]
        public string PartyName { get; set; } = string.Empty;

        [StringLength(30)]
        public string? NationalCodeOrEconomicId { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal SubTotal { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal VatAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal GrandTotal { get; set; }

        [StringLength(600)]
        public string? Notes { get; set; }

        public int? WarehouseReceiptId { get; set; }

        public int? FollowUpEmployeeId { get; set; }

        public int? EmployerId { get; set; }

        [StringLength(20)]
        public string? DeadlineDateShamsi { get; set; }

        [Required(ErrorMessage = "مبلغ فاکتور الزامی است.")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [Required(ErrorMessage = "نام فروشنده الزامی است.")]
        [StringLength(150)]
        public string VendorName { get; set; } = string.Empty;

        [Required(ErrorMessage = "تاریخ فاکتور الزامی است.")]
        public DateTime InvoiceDate { get; set; }

        [Timestamp]
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public WarehouseReceipt? WarehouseReceipt { get; set; }

        public HumanCapitalEmployee? FollowUpEmployee { get; set; }

        public Employer? Employer { get; set; }

        public List<InvoiceItem> Items { get; set; } = new();
    }
}
