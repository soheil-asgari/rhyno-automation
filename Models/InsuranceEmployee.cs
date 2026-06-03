namespace OfficeAutomation.Models
{
    public class InsuranceEmployee
    {
        public int Id { get; set; }

        public int InsuranceListId { get; set; }

        public int? HumanCapitalEmployeeId { get; set; }

        public string FullName { get; set; }

        public string JobTitle { get; set; }

        public DateTime StartWork { get; set; }

        public DateTime? EndWork { get; set; }

        public int WorkDays { get; set; }

        public decimal Salary { get; set; }

        public InsuranceList InsuranceList { get; set; }

        public HumanCapitalEmployee? HumanCapitalEmployee { get; set; }
    }
}
