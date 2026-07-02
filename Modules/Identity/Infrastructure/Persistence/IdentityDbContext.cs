using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Data;
using OfficeAutomation.Models;
using OfficeAutomation.Services.Auditing;
using OfficeAutomation.Services.Tenancy;

namespace OfficeAutomation.Modules.Identity.Infrastructure.Persistence;

public sealed class IdentityDbContext :
    Microsoft.AspNetCore.Identity.EntityFrameworkCore.IdentityDbContext<User, ApplicationRole, string>,
    ITenantSchemaDbContext,
    IAuditableDbContext
{
    private readonly ITenantIsolationService? _tenantIsolationService;
    private List<PendingAuditLogEntry> _pendingAuditEntries = [];

    public IdentityDbContext(
        DbContextOptions<IdentityDbContext> options,
        ITenantIsolationService? tenantIsolationService = null)
        : base(options)
    {
        _tenantIsolationService = tenantIsolationService;
    }

    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<RoleConflictRule> RoleConflictRules => Set<RoleConflictRule>();
    public DbSet<UserPreference> UserPreferences => Set<UserPreference>();
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

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        if (!string.IsNullOrWhiteSpace(CurrentDatabaseSchema))
        {
            builder.HasDefaultSchema(CurrentDatabaseSchema);
        }

        builder.ApplyConfigurationsFromAssembly(
            typeof(IdentityDbContext).Assembly,
            type => type.Namespace?.StartsWith("OfficeAutomation.Modules.Identity.", StringComparison.Ordinal) == true);
    }
}
