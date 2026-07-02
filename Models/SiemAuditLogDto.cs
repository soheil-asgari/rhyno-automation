namespace OfficeAutomation.Models;

public sealed class SiemAuditLogDto
{
    public Guid EventId { get; init; }
    public string EventType { get; init; } = "audit.trail";
    public DateTimeOffset OccurredAtUtc { get; init; }
    public string Severity { get; init; } = "informational";
    public string Module { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty;
    public string TableName { get; init; } = string.Empty;
    public string? EntityId { get; init; }
    public string? TenantId { get; init; }
    public string? CorrelationId { get; init; }
    public bool IsSensitive { get; init; }
    public string? IntegrityHash { get; init; }
    public SiemActorDto Actor { get; init; } = new();
    public SiemDeviceDto Device { get; init; } = new();
    public object? UserContext { get; init; }
    public object? ChangeSet { get; init; }
    public IReadOnlyList<string> ComplianceTags { get; init; } = [];
}

public sealed class SiemActorDto
{
    public string? UserId { get; init; }
    public string? DisplayName { get; init; }
    public IReadOnlyList<string> Roles { get; init; } = [];
    public IReadOnlyList<string> Permissions { get; init; } = [];
}

public sealed class SiemDeviceDto
{
    public string? IpAddress { get; init; }
    public string? UserAgent { get; init; }
}
