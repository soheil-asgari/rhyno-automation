namespace OfficeAutomation.Services.Auditing
{
    public sealed record AuditChangeSet(
        IReadOnlyList<string> AffectedColumns,
        IReadOnlyList<AuditFieldChange> Changes);
}
