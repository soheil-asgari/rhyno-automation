namespace OfficeAutomation.Services.Auditing
{
    public sealed record AuditRequestInfo(
        string? UserId,
        string? UserIP,
        string? UserAgent);
}
