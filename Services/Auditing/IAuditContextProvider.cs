namespace OfficeAutomation.Services.Auditing
{
    public interface IAuditContextProvider
    {
        AuditRequestInfo GetCurrent();
    }
}
