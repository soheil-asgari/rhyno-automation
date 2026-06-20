using System.ComponentModel.DataAnnotations;

namespace OfficeAutomation.Models
{
    public class InventoryMovementLedger
    {
        public int Id { get; set; }

        [Required]
        [StringLength(80)]
        public string DocumentType { get; set; } = string.Empty;

        [Required]
        [StringLength(80)]
        public string DocumentId { get; set; } = string.Empty;

        [Range(1, int.MaxValue)]
        public int WarehouseId { get; set; }

        [Range(1, int.MaxValue)]
        public int ProductId { get; set; }

        [Range(0, 99999999999)]
        public decimal QuantityIn { get; set; }

        [Range(0, 99999999999)]
        public decimal QuantityOut { get; set; }

        [Range(0, 99999999999)]
        public decimal BalanceAfter { get; set; }

        [StringLength(100)]
        public string? CreatedByUserId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [StringLength(300)]
        public string? Notes { get; set; }

        public Warehouse Warehouse { get; set; } = null!;

        public Product Product { get; set; } = null!;
    }
}
