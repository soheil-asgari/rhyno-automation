using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Models;
using OfficeAutomation.Services.Auditing;
using OfficeAutomation.Services.Tenancy;

namespace OfficeAutomation.Data;

public abstract class ModularDbContext : DbContext, ITenantSchemaDbContext, IAuditableDbContext
{
    private readonly ITenantIsolationService? _tenantIsolationService;
    private List<PendingAuditLogEntry> _pendingAuditEntries = [];

    protected ModularDbContext(
        DbContextOptions options,
        ITenantIsolationService? tenantIsolationService = null)
        : base(options)
    {
        _tenantIsolationService = tenantIsolationService;
    }

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public string? CurrentDatabaseSchema
    {
        get
        {
            if (_tenantIsolationService == null)
            {
                return null;
            }

            try
            {
                var descriptor = _tenantIsolationService.GetCurrent();
                return descriptor.IsolationMode.Equals(TenantIsolationMode.SchemaPerTenant, StringComparison.OrdinalIgnoreCase)
                    ? descriptor.DatabaseSchema
                    : null;
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }
    }

    public List<PendingAuditLogEntry> PrepareAuditEntries(AuditRequestInfo requestInfo)
    {
        return AuditEntryFactory.Create(this, requestInfo);
    }

    public void SetPendingAuditEntries(List<PendingAuditLogEntry> entries)
    {
        _pendingAuditEntries = entries;
    }

    public List<PendingAuditLogEntry> DequeuePendingAuditEntries()
    {
        var entries = _pendingAuditEntries;
        _pendingAuditEntries = [];
        return entries;
    }

    protected void ApplyTenantSchema(ModelBuilder modelBuilder)
    {
        if (!string.IsNullOrWhiteSpace(CurrentDatabaseSchema))
        {
            modelBuilder.HasDefaultSchema(CurrentDatabaseSchema);
        }
    }
}
