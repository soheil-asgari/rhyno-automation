namespace OfficeAutomation.Services.Auditing
{
public sealed record AuditRequestInfo(
    string? UserId,
    string? UserName,
    string? DisplayName,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions,
    string? Clearance,
    string? UserIP,
    string? UserAgent,
    string? CorrelationId);
}
