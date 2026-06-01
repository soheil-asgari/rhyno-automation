using System.ComponentModel.DataAnnotations;

namespace OfficeAutomation.Models
{
    public class HumanCapitalEmployee
    {
        public int Id { get; set; }

        [Required]
        [StringLength(30)]
        public string PersonnelCode { get; set; } = string.Empty;

        [Required]
        [StringLength(120)]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        public string NationalCode { get; set; } = string.Empty;

        public DateTime BirthDate { get; set; }

        public DateTime HireDate { get; set; }

        public DateTime? ContractEndDate { get; set; }

        public bool OnboardingCompleted { get; set; }

        public int? DepartmentId { get; set; }
        public Department? Department { get; set; }

        [Required]
        [StringLength(100)]
        public string PositionTitle { get; set; } = string.Empty;

        [Required]
        [StringLength(60)]
        public string EmploymentType { get; set; } = string.Empty;

        public decimal CurrentSalary { get; set; }

        [Required]
        [StringLength(40)]
        public string CurrentStatus { get; set; } = "فعال";

        [StringLength(20)]
        public string? PhoneNumber { get; set; }

        [StringLength(120)]
        public string? Email { get; set; }

        [StringLength(300)]
        public string? Address { get; set; }

        [StringLength(1000)]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        public List<HumanCapitalSalaryHistory> SalaryHistories { get; set; } = new();

        public List<HumanCapitalStatusHistory> StatusHistories { get; set; } = new();
    }
}
