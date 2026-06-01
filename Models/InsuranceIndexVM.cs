using System.Collections.Generic;

namespace OfficeAutomation.Models
{
    public class InsuranceIndexVM
    {
        public string? SearchTerm { get; set; }

        public int? Month { get; set; }

        public int? Year { get; set; }

        public string? Status { get; set; }

        public int TotalCount { get; set; }

        public int FilteredCount { get; set; }

        public List<InsuranceList> Items { get; set; } = new List<InsuranceList>();

        public List<string> AvailableStatuses { get; set; } = new List<string>();
    }
}
