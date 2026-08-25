using System.ComponentModel.DataAnnotations;

namespace OfficeAutomation.Models
{
    /// <summary>
    /// تنظیمات اختصاصی هر کاربر برای ماژول قراردادها
    /// </summary>
    public class ContractUserSetting
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(450)]
        public string UserId { get; set; } = string.Empty;

        public User? User { get; set; }

        [Required]
        [StringLength(40)]
        [Display(Name = "وضعیت پیش‌فرض")]
        public string DefaultStatus { get; set; } = ContractStatuses.Draft;

        [Display(Name = "مسئول پیگیری پیش‌فرض")]
        public string? DefaultResponsibleUserId { get; set; }

        public User? DefaultResponsibleUser { get; set; }

        [Display(Name = "اعلان هنگام ایجاد قرارداد")]
        public bool NotifyOnCreate { get; set; }

        /// <summary>
        /// ستون‌های انتخاب‌شده برای نمایش در لیست قراردادها (JSON array of column names).
        /// اگر null باشد، همه ستون‌ها نمایش داده می‌شوند.
        /// </summary>
        [Display(Name = "ستون‌های نمایش داده شده")]
        public string? SelectedColumns { get; set; }

        [Display(Name = "آخرین به‌روزرسانی")]
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}