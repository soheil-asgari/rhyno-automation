using System.ComponentModel.DataAnnotations;

namespace OfficeAutomation.Models
{
    public class PayrollItem
    {
        public int Id { get; set; }

        public int PayrollListId { get; set; }

        public int? HumanCapitalEmployeeId { get; set; }

        [Required]
        [StringLength(120)]
        public string EmployeeName { get; set; } = string.Empty;

        [Range(0, 99999999999)]
        public decimal BaseSalary { get; set; }

        [Range(0, 99999999999)]
        public decimal Allowance { get; set; }

        [Range(0, 99999999999)]
        public decimal Overtime { get; set; }

        [Range(0, 99999999999)]
        public decimal InsuranceDeduction { get; set; }

        [Range(0, 99999999999)]
        public decimal Tax { get; set; }

        [Range(0, 99999999999)]
        public decimal NetPayable { get; set; }

        public PayrollList PayrollList { get; set; } = null!;

        public HumanCapitalEmployee? HumanCapitalEmployee { get; set; }
    }
}
