using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using OfficeAutomation.Models;
using OfficeAutomation.Services.Auditing;

namespace OfficeAutomation.Data;

public interface IAuditableDbContext
{
    ChangeTracker ChangeTracker { get; }
    DbSet<AuditLog> AuditLogs { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    List<PendingAuditLogEntry> PrepareAuditEntries(AuditRequestInfo requestInfo);
    void SetPendingAuditEntries(List<PendingAuditLogEntry> entries);
    List<PendingAuditLogEntry> DequeuePendingAuditEntries();
}
