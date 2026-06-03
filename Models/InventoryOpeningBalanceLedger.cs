namespace OfficeAutomation.Models
{
    public class InventoryOpeningBalanceLedger
    {
        public int Id { get; set; }

        public int WarehouseId { get; set; }

        public int ProductId { get; set; }

        public int WarehouseClosingId { get; set; }

        public int PeriodYear { get; set; }

        public decimal Quantity { get; set; }

        public DateTime CreatedAt { get; set; }

        public Warehouse Warehouse { get; set; } = null!;

        public Product Product { get; set; } = null!;

        public WarehouseClosing WarehouseClosing { get; set; } = null!;
    }
}
