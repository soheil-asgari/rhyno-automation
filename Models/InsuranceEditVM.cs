using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace OfficeAutomation.Models
{
    public class InsuranceEditVM
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "نام پروژه الزامی است.")]
        [StringLength(150, ErrorMessage = "نام پروژه نمی‌تواند بیشتر از 150 کاراکتر باشد.")]
        public string ProjectName { get; set; } = string.Empty;

        [Required(ErrorMessage = "نام مدیر پروژه الزامی است.")]
        [StringLength(100, ErrorMessage = "نام مدیر نمی‌تواند بیشتر از 100 کاراکتر باشد.")]
        public string ManagerName { get; set; } = string.Empty;

        [Range(1, 12, ErrorMessage = "ماه باید بین 1 تا 12 باشد.")]
        public int Month { get; set; }

        [Range(1300, 1600, ErrorMessage = "سال وارد شده معتبر نیست.")]
        public int Year { get; set; }

        [Required(ErrorMessage = "وضعیت لیست بیمه را مشخص کنید.")]
        [StringLength(50)]
        public string Status { get; set; } = "Draft";

        public List<InsuranceEmployee> Employees { get; set; } = new List<InsuranceEmployee>();

        public List<string> AvailableStatuses { get; set; } = new List<string>();
    }
}
