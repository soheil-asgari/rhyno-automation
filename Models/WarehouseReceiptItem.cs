using System.ComponentModel.DataAnnotations;

namespace OfficeAutomation.Models
{
    public class WarehouseReceiptItem
    {
        public int Id { get; set; }

        public int WarehouseReceiptId { get; set; }

        public int ProductId { get; set; }

        [Range(0.001, 99999999999)]
        public decimal Quantity { get; set; }

        [Range(0, 99999999999)]
        public decimal UnitPrice { get; set; }

        public WarehouseReceipt WarehouseReceipt { get; set; } = null!;

        public Product Product { get; set; } = null!;
    }
}
