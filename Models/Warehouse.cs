using System.ComponentModel.DataAnnotations;

namespace OfficeAutomation.Models
{
    public class Warehouse
    {
        public int Id { get; set; }

        [Required]
        [StringLength(30)]
        public string Code { get; set; } = string.Empty;

        [Required]
        [StringLength(120)]
        public string Name { get; set; } = string.Empty;

        [StringLength(200)]
        public string? Location { get; set; }

        public string? ManagerUserId { get; set; }

        public User? ManagerUser { get; set; }

        public bool IsActive { get; set; } = true;

        public bool IsClosed { get; set; }

        public DateTime CreatedAt { get; set; }

        public List<InventoryStock> Stocks { get; set; } = new();

        public List<WarehouseReceipt> Receipts { get; set; } = new();

        public List<WarehouseIssuance> Issuances { get; set; } = new();

        public List<InventoryCounting> Countings { get; set; } = new();

        public List<WarehouseClosing> Closings { get; set; } = new();

        public List<InventoryOpeningBalanceLedger> OpeningLedgers { get; set; } = new();
    }
}
