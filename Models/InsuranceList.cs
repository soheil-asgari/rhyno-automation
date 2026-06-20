using System;

namespace OfficeAutomation.Models
{
    public class InsuranceList
    {
        public int Id { get; set; }

        public string ProjectName { get; set; } = string.Empty;

        public string ManagerName { get; set; } = string.Empty;

        public int Month { get; set; }

        public int Year { get; set; }

        public int EmployeeCount { get; set; }

        public string Status { get; set; } = string.Empty;

        public string? FilePath { get; set; }

        public DateTime CreatedDate { get; set; }

        public List<InsuranceEmployee> Employees { get; set; } = [];
    }
}
