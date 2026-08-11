using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OfficeAutomation.Models
{
    /// <summary>
    /// قالب/فرمت اختصاصی هر کاربر برای قراردادها
    /// OwnerUserId = null means Global Template (Admin only)
    /// OwnerUserId = Has Value means Personal Template for that specific employee
    /// </summary>
    public class ContractTemplate
    {
        public int Id { get; set; }

        /// <summary>
        /// Null = Global Template for all users; Has Value = Personal template for that employee
        /// </summary>
        [MaxLength(450)]
        public string? OwnerUserId { get; set; }

        public User? OwnerUser { get; set; }

        [Required(ErrorMessage = "نام قالب الزامی است.")]
        [StringLength(120, ErrorMessage = "نام قالب نمی‌تواند بیشتر از 120 کاراکتر باشد.")]
        [Display(Name = "نام قالب")]
        public string Name { get; set; } = string.Empty;

        /// <summary>قالب HTML برای چاپ/نمایش قرارداد (شامل placeholders مانند {{CustomerName}})</summary>
        [Display(Name = "قالب HTML")]
        public string? HtmlContent { get; set; }

        /// <summary>نوع قالب (Global/Personal)</summary>
        [StringLength(20)]
        public string? TemplateType { get; set; } // "Global" or "Personal"

        [StringLength(500)]
        [Display(Name = "توضیحات")]
        public string? Description { get; set; }

        [Display(Name = "قالب پیش‌فرض")]
        public bool IsDefault { get; set; }

        [Display(Name = "تاریخ ایجاد")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Display(Name = "آخرین ویرایش")]
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        public List<Contract> Contracts { get; set; } = new();
    }
}