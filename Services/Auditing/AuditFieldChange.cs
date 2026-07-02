namespace OfficeAutomation.Services.Auditing
{
    public sealed record AuditFieldChange(string Field, object? OldValue, object? NewValue);
}
