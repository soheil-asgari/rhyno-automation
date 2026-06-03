namespace OfficeAutomation.Models
{
    public class WarehouseClosingItem
    {
        public int Id { get; set; }

        public int WarehouseClosingId { get; set; }

        public int ProductId { get; set; }

        public decimal ClosingQuantity { get; set; }

        public decimal OpeningQuantity { get; set; }

        public WarehouseClosing WarehouseClosing { get; set; } = null!;

        public Product Product { get; set; } = null!;
    }
}
