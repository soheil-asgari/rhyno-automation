using System.ComponentModel.DataAnnotations;

namespace OfficeAutomation.Models
{
    public class PayrollList
    {
        public int Id { get; set; }

        [Range(1, 12)]
        public int Month { get; set; }

        [Range(1300, 1600)]
        public int Year { get; set; }

        [StringLength(50)]
        public string Status { get; set; } = "Draft";

        public bool IsFinalized { get; set; }

        [Timestamp]
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        public List<PayrollItem> Items { get; set; } = new();
    }
}
