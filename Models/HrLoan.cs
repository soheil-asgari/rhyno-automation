using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OfficeAutomation.Models
{
    public class HrLoan
    {
        public int Id { get; set; }

        [Required]
        public int EmployeeId { get; set; }

        [ForeignKey(nameof(EmployeeId))]
        public HumanCapitalEmployee? Employee { get; set; }

        [Required]
        public DateTime RequestDate { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        public int InstallmentCount { get; set; }

        [Required]
        [StringLength(30)]
        public string Status { get; set; } = "در انتظار";

        [StringLength(20)]
        public string TenantId { get; set; } = string.Empty;

        public bool IsDeleted { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        [StringLength(500)]
        public string? Description { get; set; }
    }
}