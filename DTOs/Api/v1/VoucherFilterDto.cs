namespace OfficeAutomation.DTOs.Api.v1
{
    public class VoucherFilterDto
    {
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 25;
        public int? VoucherNo { get; set; }
        public string? VoucherDate { get; set; }
        public string? Status { get; set; }
        public string? VoucherType { get; set; }
        public string? Description { get; set; }
        public int? ReferenceNo { get; set; }
        public int? DailyNo { get; set; }
        public decimal? Amount { get; set; }
        public string? CreatedBy { get; set; }
    }
}