namespace OfficeAutomation.Services.Auditing
{
    public sealed record AuditUserContext(
        string? UserId,
        string? UserName,
        string? DisplayName,
        int? DepartmentId,
        IReadOnlyList<string> Roles,
        IReadOnlyList<string> Permissions,
        string? IpAddress,
        string? UserAgent);
}
