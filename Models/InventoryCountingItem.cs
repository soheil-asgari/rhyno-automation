using System.ComponentModel.DataAnnotations;

namespace OfficeAutomation.Models
{
    public class InventoryCountingItem
    {
        public int Id { get; set; }

        public int InventoryCountingId { get; set; }

        public int ProductId { get; set; }

        [Range(0, 99999999999)]
        public decimal SystemQuantity { get; set; }

        [Range(0, 99999999999)]
        public decimal PhysicalQuantity { get; set; }

        public decimal DiscrepancyQuantity { get; set; }

        public InventoryCounting InventoryCounting { get; set; } = null!;

        public Product Product { get; set; } = null!;
    }
}
