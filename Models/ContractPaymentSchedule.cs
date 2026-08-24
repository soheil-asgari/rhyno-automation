using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OfficeAutomation.Models
{
    /// <summary>
    /// برنامه زمان‌بندی پرداخت قرارداد (Payment Schedule)
    /// </summary>
    public class ContractPaymentSchedule
    {
        public int Id { get; set; }
        [Required] public int ContractId { get; set; }
        public Contract? Contract { get; set; }

        [Required][StringLength(20)]
        [Display(Name = "نوع برنامه")]
        public string ScheduleType { get; set; } = "Fixed";

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "مبلغ کل")]
        public decimal TotalAmount { get; set; }

        [Display(Name = "تعداد اقساط")]
        public int InstallmentCount { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        [StringLength(450)] public string CreatedByUserId { get; set; } = string.Empty;
        public User? CreatedByUser { get; set; }

        public List<ContractPaymentScheduleInstallment> Installments { get; set; } = new();
    }

    /// <summary>
    /// قسط/ردیف برنامه پرداخت
    /// </summary>
    public class ContractPaymentScheduleInstallment
    {
        public int Id { get; set; }
        [Required] public int ScheduleId { get; set; }
        public ContractPaymentSchedule? Schedule { get; set; }

        [Display(Name = "شماره قسط")]
        public int InstallmentNumber { get; set; }

        [Required][DataType(DataType.Date)]
        [Display(Name = "تاریخ سررسید")]
        public DateTime DueDate { get; set; }

        [Required][Column(TypeName = "decimal(18,2)")]
        [Display(Name = "مبلغ قسط")]
        public decimal Amount { get; set; }

        [Column(TypeName = "decimal(5,2)")]
        [Display(Name = "درصد")]
        public decimal? Percentage { get; set; }

        [StringLength(300)][Display(Name = "توضیحات")]
        public string? Description { get; set; }

        [Required][StringLength(20)]
        [Display(Name = "وضعیت")]
        public string Status { get; set; } = "Pending";

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "مبلغ پرداخت‌شده")]
        public decimal PaidAmount { get; set; }

        public int? PaymentId { get; set; }
        public ContractPayment? Payment { get; set; }
    }

    // ── ViewModels ────────────────────────────────────────────────

    public class PaymentScheduleVM
    {
        public int ContractId { get; set; }
        public string ContractNumber { get; set; } = string.Empty;
        public decimal ContractTotalAmount { get; set; }

        /// <summary>شناسه برنامه (برای حذف و سفارشی‌سازی)</summary>
        public int ScheduleId { get; set; }

        [Required][Display(Name = "نوع برنامه")]
        public string ScheduleType { get; set; } = "Fixed";

        [Required][Range(1, 60, ErrorMessage = "تعداد اقساط باید بین 1 تا 60 باشد.")]
        [Display(Name = "تعداد اقساط")]
        public int InstallmentCount { get; set; } = 1;

        [Required][DataType(DataType.Date)]
        [Display(Name = "تاریخ اولین قسط")]
        public DateTime StartDate { get; set; } = DateTime.Today;

        [Required][Range(1, 365)]
        [Display(Name = "فاصله (روز)")]
        public int IntervalDays { get; set; } = 30;

        public List<PaymentScheduleInstallmentVM> Installments { get; set; } = new();
    }

    public class PaymentScheduleInstallmentVM
    {
        public int Id { get; set; }
        public int InstallmentNumber { get; set; }
        public DateTime DueDate { get; set; }
        public decimal Amount { get; set; }
        public decimal? Percentage { get; set; }
        public string? Description { get; set; }
        public string Status { get; set; } = "Pending";
        public decimal PaidAmount { get; set; }
        public int? PaymentId { get; set; }
        public decimal CumulativeAmount { get; set; }
        public bool IsOverdue => Status == "Pending" && DueDate < DateTime.Today;
    }
}
