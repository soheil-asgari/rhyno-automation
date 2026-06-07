namespace OfficeAutomation.Models
{
    public sealed class AuditLogListItemDto
    {
        public Guid Id { get; init; }
        public string? UserId { get; init; }
        public string? UserDisplayName { get; init; }
        public string Action { get; init; } = string.Empty;
        public string TableName { get; init; } = string.Empty;
        public DateTimeOffset DateTime { get; init; }
        public string? OldValues { get; init; }
        public string? NewValues { get; init; }
        public string? AffectedColumns { get; init; }
        public string? UserIP { get; init; }
        public string? UserAgent { get; init; }
    }

    public sealed class AuditLogFilterOptionDto
    {
        public string Value { get; init; } = string.Empty;
        public string Label { get; init; } = string.Empty;
    }

    public sealed class AuditLogFilterOptionsDto
    {
        public IReadOnlyList<AuditLogFilterOptionDto> Users { get; init; } = [];
        public IReadOnlyList<string> Actions { get; init; } = [];
        public IReadOnlyList<string> TableNames { get; init; } = [];
    }

    public sealed class PagedResult<T>
    {
        public required IReadOnlyList<T> Items { get; init; }
        public required int Page { get; init; }
        public required int PageSize { get; init; }
        public required int TotalCount { get; init; }
        public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    }
}
