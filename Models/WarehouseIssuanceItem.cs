using System.ComponentModel.DataAnnotations;

namespace OfficeAutomation.Models
{
    public class WarehouseIssuanceItem
    {
        public int Id { get; set; }

        public int WarehouseIssuanceId { get; set; }

        public int ProductId { get; set; }

        [Range(0.001, 99999999999)]
        public decimal Quantity { get; set; }

        public WarehouseIssuance WarehouseIssuance { get; set; } = null!;

        public Product Product { get; set; } = null!;
    }
}
