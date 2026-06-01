using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OfficeAutomation.Models
{
    public class Waybill
    {
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string WaybillNumber { get; set; } = string.Empty;

        public DateTime IssueDate { get; set; }

        public DateTime LoadingDate { get; set; }

        [Required]
        [StringLength(150)]
        public string SenderName { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string OriginCity { get; set; } = string.Empty;

        [Required]
        [StringLength(150)]
        public string ReceiverName { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string DestinationCity { get; set; } = string.Empty;

        [Required]
        [StringLength(120)]
        public string DriverName { get; set; } = string.Empty;

        [Required]
        [StringLength(10)]
        public string DriverNationalId { get; set; } = string.Empty;

        [Required]
        [StringLength(15)]
        public string DriverPhone { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        public string VehiclePlateNumber { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string VehicleType { get; set; } = string.Empty;

        [Required]
        [StringLength(120)]
        public string CargoType { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,3)")]
        public decimal Weight { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalFreightCharges { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal DriverCommission { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal NetPayToDriver { get; set; }

        [Required]
        [StringLength(30)]
        public string PaymentStatus { get; set; } = "Pending";

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? UpdatedAt { get; set; }

        public bool IsDeleted { get; set; }

        public DateTime? DeletedAt { get; set; }
    }
}
