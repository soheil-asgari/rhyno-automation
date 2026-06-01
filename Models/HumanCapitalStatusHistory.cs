using System.ComponentModel.DataAnnotations;

namespace OfficeAutomation.Models
{
    public class HumanCapitalStatusHistory
    {
        public int Id { get; set; }

        public int EmployeeId { get; set; }
        public HumanCapitalEmployee Employee { get; set; } = null!;

        [Required]
        [StringLength(40)]
        public string StatusType { get; set; } = string.Empty;

        public DateTime EffectiveDate { get; set; }

        [StringLength(120)]
        public string? ReferenceNumber { get; set; }

        [Required]
        [StringLength(500)]
        public string Description { get; set; } = string.Empty;

        [StringLength(500)]
        public string? ExitReason { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
