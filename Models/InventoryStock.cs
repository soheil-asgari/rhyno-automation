using System.ComponentModel.DataAnnotations;

namespace OfficeAutomation.Models
{
    public class InventoryStock
    {
        public int Id { get; set; }

        public int ProductId { get; set; }

        [Range(1, int.MaxValue)]
        public int WarehouseId { get; set; } = 1;

        [Range(0, 99999999999)]
        public decimal CurrentQuantity { get; set; }

        [Timestamp]
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();

        public DateTime UpdatedAt { get; set; }

        public Product Product { get; set; } = null!;

        public Warehouse Warehouse { get; set; } = null!;
    }
}
