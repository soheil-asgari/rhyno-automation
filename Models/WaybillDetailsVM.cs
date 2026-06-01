namespace OfficeAutomation.Models
{
    public class WaybillDetailsVM
    {
        public int Id { get; set; }

        public string WaybillNumber { get; set; } = string.Empty;

        public DateTime IssueDate { get; set; }

        public DateTime LoadingDate { get; set; }

        public string SenderName { get; set; } = string.Empty;

        public string OriginCity { get; set; } = string.Empty;

        public string ReceiverName { get; set; } = string.Empty;

        public string DestinationCity { get; set; } = string.Empty;

        public string DriverName { get; set; } = string.Empty;

        public string DriverNationalId { get; set; } = string.Empty;

        public string DriverPhone { get; set; } = string.Empty;

        public string VehiclePlateNumber { get; set; } = string.Empty;

        public string VehicleType { get; set; } = string.Empty;

        public string CargoType { get; set; } = string.Empty;

        public decimal Weight { get; set; }

        public decimal TotalFreightCharges { get; set; }

        public decimal DriverCommission { get; set; }

        public decimal NetPayToDriver { get; set; }

        public string PaymentStatus { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }
    }
}
