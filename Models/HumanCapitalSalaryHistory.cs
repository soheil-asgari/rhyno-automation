using System.ComponentModel.DataAnnotations;

namespace OfficeAutomation.Models
{
    public class HumanCapitalSalaryHistory
    {
        public int Id { get; set; }

        public int EmployeeId { get; set; }
        public HumanCapitalEmployee Employee { get; set; } = null!;

        public DateTime EffectiveDate { get; set; }

        public decimal PreviousSalary { get; set; }

        public decimal NewSalary { get; set; }

        [StringLength(120)]
        public string? PromotionTitle { get; set; }

        [Required]
        [StringLength(500)]
        public string Reason { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }
    }
}
