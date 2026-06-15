using System.ComponentModel.DataAnnotations;

namespace OfficeAutomation.Models
{
    public class WarehouseReceipt
    {
        public int Id { get; set; }

        [Required]
        [StringLength(40)]
        public string ReceiptNumber { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        public string DateShamsi { get; set; } = string.Empty;

        [Required]
        [StringLength(200)]
        public string SupplierOrSource { get; set; } = string.Empty;

        public int? VendorId { get; set; }

        [StringLength(600)]
        public string? Notes { get; set; }

        [StringLength(30)]
        public string WorkflowStatus { get; set; } = Models.WorkflowStatus.Approved;

        public int WarehouseId { get; set; }

        public DateTime CreatedAt { get; set; }

        public Warehouse Warehouse { get; set; } = null!;

        public Vendor? Vendor { get; set; }

        public List<WarehouseReceiptItem> Items { get; set; } = new();
    }
}
