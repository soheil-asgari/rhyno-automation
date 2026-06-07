using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace OfficeAutomation.Models
{
   
    public class User : IdentityUser
    {
        [Required]
        public string? FullName { get; set; }

        // تغییر از internal به public برای ثبت در دیتابیس
        public string? JobTitle { get; set; }

        public string? Role { get; set; }

        public string? SignaturePath { get; set; }

        public string? Gender { get; set; }
        public string? ServiceLocation { get; set; }
        public string? ManagerId { get; set; }
        public virtual User? Manager { get; set; }
        public string? ParentManagerUserId { get; set; }
        public virtual User? ParentManagerUser { get; set; }
        public int? DepartmentId { get; set; }

        public Department? Department { get; set; }

        public int? EmployeeId { get; set; }

        public HumanCapitalEmployee? Employee { get; set; }

        public bool CanAccessFinance { get; set; }

        public bool CanAccessWarehouse { get; set; }

        public bool CanAccessHumanCapital { get; set; }

        public bool CanAccessSystemSettings { get; set; }

        public bool IsManager { get; set; }



    }
}
