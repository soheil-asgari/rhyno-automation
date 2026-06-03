using System.ComponentModel.DataAnnotations;

namespace OfficeAutomation.Models
{
    public class Vendor
    {
        public int Id { get; set; }

        [Required]
        [StringLength(150)]
        public string Name { get; set; } = string.Empty;

        [StringLength(50)]
        public string? EconomicCode { get; set; }

        [StringLength(20)]
        public string? NationalId { get; set; }

        [StringLength(20)]
        public string? Phone { get; set; }

        [StringLength(300)]
        public string? Address { get; set; }

        public bool IsActive { get; set; } = true;

        public List<WarehouseReceipt> Receipts { get; set; } = new();
    }
}
