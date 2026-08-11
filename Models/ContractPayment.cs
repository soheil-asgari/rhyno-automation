using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OfficeAutomation.Models
{
    /// <summary>
    /// ردیف پرداخت‌های انجام شده برای یک قرارداد
    /// </summary>
    public class ContractPayment
    {
        public int Id { get; set; }

        [Required]
        public int ContractId { get; set; }

        public Contract? Contract { get; set; }

        [Required(ErrorMessage = "مبلغ پرداخت الزامی است.")]
        [Range(0, 999999999999, ErrorMessage = "مبلغ پرداخت معتبر نیست.")]
        [Display(Name = "مبلغ پرداخت")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [Required(ErrorMessage = "تاریخ پرداخت الزامی است.")]
        [DataType(DataType.Date)]
        [Display(Name = "تاریخ پرداخت")]
        public DateTime PaymentDate { get; set; } = DateTime.Today;

        [StringLength(60)]
        [Display(Name = "روش پرداخت")]
        public string? PaymentMethod { get; set; }

        [StringLength(120)]
        [Display(Name = "شماره مرجع")]
        public string? ReferenceNumber { get; set; }

        [StringLength(500)]
        [Display(Name = "توضیحات")]
        public string? Description { get; set; }

        [Display(Name = "تاریخ ثبت")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}