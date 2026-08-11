using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OfficeAutomation.Models
{
    /// <summary>
    /// قالب/فرمت اختصاصی هر کاربر برای قراردادها
    /// هر کاربر می‌تواند قالب منحصر به فرد خود را داشته باشد
    /// </summary>
    public class ContractTemplate
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(450)]
        public string UserId { get; set; } = string.Empty;

        public User? User { get; set; }

        [Required(ErrorMessage = "نام قالب الزامی است.")]
        [StringLength(120, ErrorMessage = "نام قالب نمی‌تواند بیشتر از 120 کاراکتر باشد.")]
        [Display(Name = "نام قالب")]
        public string Name { get; set; } = string.Empty;

        /// <summary>تنظیمات فرمت به صورت JSON (شامل فیلدها، ترتیب، رنگ و ...)</summary>
        [Display(Name = "تنظیمات فرمت (JSON)")]
        public string? FormatJson { get; set; }

        /// <summary>قالب HTML برای چاپ/نمایش قرارداد</summary>
        [Display(Name = "قالب HTML")]
        public string? TemplateHtml { get; set; }

        /// <summary>آیا این قالب پیش‌فرض کاربر است</summary>
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