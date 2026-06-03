using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace OfficeAutomation.Models
{
    public class InsuranceCreateVM
    {
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

        public List<InsuranceEmployeeRowViewModel> Employees { get; set; } = new();
    }
}
