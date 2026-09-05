namespace OfficeAutomation.Models
{
    public class Voucher
    {
        public long Id { get; set; }
        public int VoucherNo { get; set; }
        public string VoucherDate { get; set; } = "";
        public string Status { get; set; } = "موقت";
        public string VoucherType { get; set; } = "";
        public string? Description { get; set; }
        public int? ReferenceNo { get; set; }
        public int? DailyNo { get; set; }
        public decimal TotalAmount { get; set; }
        public string CreatedBy { get; set; } = "";
        public string? ModifiedBy { get; set; }
        public string? CheckedBy { get; set; }
    }
}