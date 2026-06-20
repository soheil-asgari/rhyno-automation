using System.Collections.Generic;

namespace OfficeAutomation.Models
{
    public class Department
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? ManagerId { get; set; }

        public User? Manager { get; set; }

        public int? ManagerEmployeeId { get; set; }

        public HumanCapitalEmployee? ManagerEmployee { get; set; }

        public ICollection<User>? Users { get; set; }
    }
}
