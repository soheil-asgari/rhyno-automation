namespace OfficeAutomation.Models
{
    public class WaybillIndexVM
    {
        public string? SearchTerm { get; set; }

        public string? PaymentStatus { get; set; }

        public int TotalCount { get; set; }

        public int FilteredCount { get; set; }

        public List<WaybillListItemVM> Items { get; set; } = new();

        public List<string> AvailablePaymentStatuses { get; set; } = new();
    }

    public class WaybillListItemVM
    {
        public int Id { get; set; }

        public string WaybillNumber { get; set; } = string.Empty;

        public DateTime IssueDate { get; set; }

        public string OriginCity { get; set; } = string.Empty;

        public string DestinationCity { get; set; } = string.Empty;

        public string DriverName { get; set; } = string.Empty;

        public decimal NetPayToDriver { get; set; }

        public string PaymentStatus { get; set; } = string.Empty;
    }
}
