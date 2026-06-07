using System.ComponentModel.DataAnnotations;

namespace OfficeAutomation.Models
{
    public class InventoryTransferRequest
    {
        public int Id { get; set; }

        [Range(1, int.MaxValue)]
        public int SourceWarehouseId { get; set; }

        public Warehouse? SourceWarehouse { get; set; }

        [Range(1, int.MaxValue)]
        public int DestinationWarehouseId { get; set; }

        public Warehouse? DestinationWarehouse { get; set; }

        [Range(1, int.MaxValue)]
        public int ProductId { get; set; }

        public Product? Product { get; set; }

        [Range(0.001, 99999999999)]
        public decimal Quantity { get; set; }

        [Required]
        [StringLength(30)]
        public string Status { get; set; } = "PendingManager";

        [Required]
        public string RequestedByUserId { get; set; } = string.Empty;

        public User? RequestedByUser { get; set; }

        public string? ApprovedByUserId { get; set; }

        public User? ApprovedByUser { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? ApprovedAt { get; set; }
    }
}
