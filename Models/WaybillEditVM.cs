using System.ComponentModel.DataAnnotations;

namespace OfficeAutomation.Models
{
    public class WaybillEditVM
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "شماره بارنامه الزامی است.")]
        [StringLength(50, ErrorMessage = "شماره بارنامه نمی‌تواند بیشتر از 50 کاراکتر باشد.")]
        [Display(Name = "شماره بارنامه")]
        public string WaybillNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "تاریخ صدور الزامی است.")]
        [Display(Name = "تاریخ صدور")]
        [DataType(DataType.Date)]
        public DateTime IssueDate { get; set; }

        [Required(ErrorMessage = "تاریخ بارگیری الزامی است.")]
        [Display(Name = "تاریخ بارگیری")]
        [DataType(DataType.Date)]
        public DateTime LoadingDate { get; set; }

        [Required(ErrorMessage = "نام فرستنده الزامی است.")]
        [StringLength(150, ErrorMessage = "نام فرستنده نمی‌تواند بیشتر از 150 کاراکتر باشد.")]
        [Display(Name = "فرستنده")]
        public string SenderName { get; set; } = string.Empty;

        [Required(ErrorMessage = "شهر مبدا الزامی است.")]
        [StringLength(100, ErrorMessage = "نام مبدا نمی‌تواند بیشتر از 100 کاراکتر باشد.")]
        [Display(Name = "مبدا")]
        public string OriginCity { get; set; } = string.Empty;

        [Required(ErrorMessage = "نام گیرنده الزامی است.")]
        [StringLength(150, ErrorMessage = "نام گیرنده نمی‌تواند بیشتر از 150 کاراکتر باشد.")]
        [Display(Name = "گیرنده")]
        public string ReceiverName { get; set; } = string.Empty;

        [Required(ErrorMessage = "شهر مقصد الزامی است.")]
        [StringLength(100, ErrorMessage = "نام مقصد نمی‌تواند بیشتر از 100 کاراکتر باشد.")]
        [Display(Name = "مقصد")]
        public string DestinationCity { get; set; } = string.Empty;

        [Required(ErrorMessage = "نام راننده الزامی است.")]
        [StringLength(120, ErrorMessage = "نام راننده نمی‌تواند بیشتر از 120 کاراکتر باشد.")]
        [Display(Name = "نام راننده")]
        public string DriverName { get; set; } = string.Empty;

        [Required(ErrorMessage = "کد ملی راننده الزامی است.")]
        [RegularExpression("^[0-9]{10}$", ErrorMessage = "کد ملی باید 10 رقم باشد.")]
        [Display(Name = "کد ملی راننده")]
        public string DriverNationalId { get; set; } = string.Empty;

        [Required(ErrorMessage = "شماره تلفن راننده الزامی است.")]
        [StringLength(15, MinimumLength = 10, ErrorMessage = "شماره تلفن باید بین 10 تا 15 کاراکتر باشد.")]
        [Display(Name = "تلفن راننده")]
        public string DriverPhone { get; set; } = string.Empty;

        [Required(ErrorMessage = "شماره پلاک خودرو الزامی است.")]
        [StringLength(20, ErrorMessage = "شماره پلاک نمی‌تواند بیشتر از 20 کاراکتر باشد.")]
        [Display(Name = "پلاک خودرو")]
        public string VehiclePlateNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "نوع خودرو الزامی است.")]
        [StringLength(50, ErrorMessage = "نوع خودرو نمی‌تواند بیشتر از 50 کاراکتر باشد.")]
        [Display(Name = "نوع خودرو")]
        public string VehicleType { get; set; } = string.Empty;

        [Required(ErrorMessage = "نوع بار الزامی است.")]
        [StringLength(120, ErrorMessage = "نوع بار نمی‌تواند بیشتر از 120 کاراکتر باشد.")]
        [Display(Name = "نوع بار")]
        public string CargoType { get; set; } = string.Empty;

        [Range(0.001, 1000000, ErrorMessage = "وزن باید بیشتر از صفر باشد.")]
        [Display(Name = "وزن")]
        public decimal Weight { get; set; }

        [Range(0, 1000000000000, ErrorMessage = "مبلغ کل کرایه معتبر نیست.")]
        [Display(Name = "مبلغ کل کرایه")]
        public decimal TotalFreightCharges { get; set; }

        [Range(0, 1000000000000, ErrorMessage = "مبلغ کمیسیون معتبر نیست.")]
        [Display(Name = "کمیسیون راننده")]
        public decimal DriverCommission { get; set; }

        [Range(0, 1000000000000, ErrorMessage = "صافی دریافتی راننده معتبر نیست.")]
        [Display(Name = "صافی دریافتی راننده")]
        public decimal NetPayToDriver { get; set; }

        [Required(ErrorMessage = "وضعیت پرداخت را مشخص کنید.")]
        [StringLength(30, ErrorMessage = "وضعیت پرداخت نمی‌تواند بیشتر از 30 کاراکتر باشد.")]
        [Display(Name = "وضعیت پرداخت")]
        public string PaymentStatus { get; set; } = "Pending";

        public List<string> AvailablePaymentStatuses { get; set; } = new() { "Paid", "Pending", "Internal" };

        public List<string> AvailableVehicleTypes { get; set; } = new() { "تریلی", "خاور", "کامیون" };
    }
}
