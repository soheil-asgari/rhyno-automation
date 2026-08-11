using System.ComponentModel.DataAnnotations;

namespace OfficeAutomation.Models
{
    /// <summary>
    /// تاریخچه/لاگ تغییرات وضعیت و ویرایش‌های قرارداد
    /// </summary>
    public class ContractAuditLog
    {
        public int Id { get; set; }

        [Required]
        public int ContractId { get; set; }

        public Contract? Contract { get; set; }

        [Required]
        [StringLength(450)]
        [Display(Name = "کاربر")]
        public string UserId { get; set; } = string.Empty;

        public User? User { get; set; }

        [Required]
        [StringLength(50)]
        [Display(Name = "عملیات")]
        public string Action { get; set; } = string.Empty; // e.g., "StatusChanged", "Edited", "PaymentAdded", "Archived", "Created"

        [StringLength(40)]
        [Display(Name = "وضعیت قبلی")]
        public string? OldStatus { get; set; }

        [StringLength(40)]
        [Display(Name = "وضعیت جدید")]
        public string? NewStatus { get; set; }

        [StringLength(2000)]
        [Display(Name = "توضیحات")]
        public string? Description { get; set; }

        [Display(Name = "تاریخ")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}