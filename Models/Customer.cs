using System.ComponentModel.DataAnnotations;

namespace OfficeAutomation.Models
{
    /// <summary>
    /// مشتری/طرف قرارداد - هر مشتری می‌تواند چندین قرارداد داشته باشد
    /// </summary>
    public class Customer
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "نام مشتری الزامی است.")]
        [StringLength(150, ErrorMessage = "نام مشتری نمی‌تواند بیشتر از 150 کاراکتر باشد.")]
        [Display(Name = "نام مشتری")]
        public string Name { get; set; } = string.Empty;

        [StringLength(50)]
        [Display(Name = "کد اقتصادی")]
        public string? EconomicCode { get; set; }

        [StringLength(20)]
        [Display(Name = "شماره ملی")]
        public string? NationalId { get; set; }

        [StringLength(20)]
        [Display(Name = "تلفن")]
        public string? Phone { get; set; }

        [StringLength(300)]
        [Display(Name = "آدرس")]
        public string? Address { get; set; }

        [StringLength(100)]
        [Display(Name = "ایمیل")]
        public string? Email { get; set; }

        [StringLength(200)]
        [Display(Name = "نام شرکت")]
        public string? CompanyName { get; set; }

        [Display(Name = "فعال")]
        public bool IsActive { get; set; } = true;

        [Display(Name = "تاریخ ایجاد")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Display(Name = "آخرین ویرایش")]
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        public List<Contract> Contracts { get; set; } = new();
    }
}