using System.ComponentModel.DataAnnotations;

namespace OfficeAutomation.Models
{
    public class Product
    {
        public int Id { get; set; }

        [Required]
        [StringLength(40)]
        public string Code { get; set; } = string.Empty;

        [Required]
        [StringLength(150)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [StringLength(30)]
        public string Unit { get; set; } = string.Empty;

        [StringLength(600)]
        public string? Description { get; set; }

        public bool IsActive { get; set; } = true;

        public bool IsDeleted { get; set; }

        public DateTime CreatedAt { get; set; }

        public List<WarehouseReceiptItem> ReceiptItems { get; set; } = new();

        public List<WarehouseIssuanceItem> IssuanceItems { get; set; } = new();

        public List<InventoryStock> Stocks { get; set; } = new();

        public List<InventoryCountingItem> CountingItems { get; set; } = new();
    }
}
