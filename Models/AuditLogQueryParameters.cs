namespace OfficeAutomation.Models
{
    public sealed class AuditLogQueryParameters
    {
        public string? UserId { get; set; }
        public string? Action { get; set; }
        public string? TableName { get; set; }
        public DateTimeOffset? From { get; set; }
        public DateTimeOffset? To { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 25;
    }
}
