using System.Collections.Generic;

namespace OfficeAutomation.Models
{
    public class InsuranceCreateVM
    {
        public string ProjectName { get; set; }

        public string ManagerName { get; set; }

        public int Month { get; set; }

        public int Year { get; set; }

        public List<InsuranceEmployee> Employees { get; set; } = new List<InsuranceEmployee>();
    }
}
