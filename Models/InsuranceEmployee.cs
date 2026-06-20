namespace OfficeAutomation.Models
{
    public class InsuranceEmployee
    {
        public int Id { get; set; }

        public int InsuranceListId { get; set; }

        public int? HumanCapitalEmployeeId { get; set; }

        public string FullName { get; set; } = string.Empty;

        public string JobTitle { get; set; } = string.Empty;

        public DateTime StartWork { get; set; }

        public DateTime? EndWork { get; set; }

        public int WorkDays { get; set; }

        public decimal Salary { get; set; }

        public InsuranceList InsuranceList { get; set; } = null!;

        public HumanCapitalEmployee? HumanCapitalEmployee { get; set; }
    }
}
