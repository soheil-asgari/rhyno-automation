using System.ComponentModel.DataAnnotations;

namespace OfficeAutomation.Models
{
    /// <summary>
    /// ارجاع تایید از یک کاربر به کاربر دیگر (Delegation) زمانی که تاییدکننده اصلی در دسترس نیست.
    /// </summary>
    public class ContractApprovalDelegation
    {
        public int Id { get; set; }

        /// <summary>کاربر اصلی که اختیار تایید را به دیگری واگذار می‌کند</summary>
        [Required]
        [StringLength(450)]
        public string FromUserId { get; set; } = string.Empty;

        public User? FromUser { get; set; }

        /// <summary>کاربری که اختیار تایید به او واگذار شده</summary>
        [Required]
        [StringLength(450)]
        public string ToUserId { get; set; } = string.Empty;

        public User? ToUser { get; set; }

        /// <summary>اختیاری: محدود به نوع قرارداد خاص</summary>
        [StringLength(60)]
        public string? ContractType { get; set; }

        /// <summary>حداکثر مبلغ قابل تایید توسط نماینده</summary>
        // public decimal? MaxAmount { get; set; } // removed to keep minimal for now

        /// <summary>تاریخ شروع اعتبار delegation</summary>
        public DateTime StartsAt { get; set; }

        /// <summary>تاریخ پایان اعتبار delegation</summary>
        public DateTime EndsAt { get; set; }

        /// <summary>فعال یا غیرفعال</summary>
        public bool IsActive { get; set; } = true;

        /// <summary>توضیح دلیل ارجاع</summary>
        [StringLength(500)]
        public string? Reason { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}