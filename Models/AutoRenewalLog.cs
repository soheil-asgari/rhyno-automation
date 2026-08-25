using System.ComponentModel.DataAnnotations;

namespace OfficeAutomation.Models
{
    /// <summary>
    /// نمایانگر یک رویداد تمدید خودکار قرارداد.
    /// هر بار که تمدید خودکار اجرا می‌شود، یک رکورد جدید ثبت می‌گردد.
    /// </summary>
    public class AutoRenewalLog
    {
        public int Id { get; set; }

        [Required]
        public int ContractId { get; set; }

        public Contract? Contract { get; set; }

        /// <summary>قرارداد جدید (تمدید شده) که ایجاد شده است</summary>
        public int? RenewedContractId { get; set; }

        /// <summary>تاریخ اجرای تمدید خودکار</summary>
        public DateTime RenewedAt { get; set; } = DateTime.Now;

        /// <summary>تاریخ پایان قبلی</summary>
        [DataType(DataType.Date)]
        public DateTime? PreviousEndDate { get; set; }

        /// <summary>تاریخ پایان جدید</summary>
        [DataType(DataType.Date)]
        public DateTime? NewEndDate { get; set; }

        /// <summary>مدت تمدید (ماه)</summary>
        public int RenewalTermMonths { get; set; }

        /// <summary>وضعیت تمدید (Success, CancelledByUser, Failed)</summary>
        [Required]
        [StringLength(30)]
        public string Status { get; set; } = "Success";

        /// <summary>توضیحات</summary>
        [StringLength(1000)]
        public string? Notes { get; set; }

        /// <summary>کاربری که در صورت لغو دستی، لغو کرده است</summary>
        [StringLength(450)]
        public string? CancelledByUserId { get; set; }

        public User? CancelledByUser { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}