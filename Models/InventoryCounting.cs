using System.ComponentModel.DataAnnotations;

namespace OfficeAutomation.Models
{
    public class InventoryCounting
    {
        public int Id { get; set; }

        [Required]
        [StringLength(40)]
        public string DocumentNumber { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        public string DateShamsi { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        public string Status { get; set; } = "Draft";

        [StringLength(600)]
        public string? Notes { get; set; }

        public int WarehouseId { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? ApprovedAt { get; set; }

        public Warehouse Warehouse { get; set; } = null!;

        public List<InventoryCountingItem> Items { get; set; } = new();
    }
}
