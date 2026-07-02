using OfficeAutomation.Data;
using OfficeAutomation.Models;

namespace OfficeAutomation.Services.Auditing
{
    public interface IAuditLogger
    {
        AuditLog Create(PendingAuditLogEntry entry);
    }
}
