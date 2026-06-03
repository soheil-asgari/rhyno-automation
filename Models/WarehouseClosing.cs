using System.ComponentModel.DataAnnotations;

namespace OfficeAutomation.Models
{
    public class WarehouseClosing
    {
        public int Id { get; set; }

        public int WarehouseId { get; set; }

        [Required]
        [StringLength(40)]
        public string DocumentNumber { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        public string ClosingDateShamsi { get; set; } = string.Empty;

        public int ClosingYear { get; set; }

        public int OpeningYear { get; set; }

        public DateTime CreatedAt { get; set; }

        public Warehouse Warehouse { get; set; } = null!;

        public List<WarehouseClosingItem> Items { get; set; } = new();
    }
}
