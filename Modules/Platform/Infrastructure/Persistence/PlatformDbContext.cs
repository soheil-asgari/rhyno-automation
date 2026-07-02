using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Data;
using OfficeAutomation.Models;
using OfficeAutomation.Modules.Platform.Domain;
using OfficeAutomation.Services.Tenancy;

namespace OfficeAutomation.Modules.Platform.Infrastructure.Persistence;

public sealed class PlatformDbContext : ModularDbContext
{
    public PlatformDbContext(
        DbContextOptions<PlatformDbContext> options,
        ITenantIsolationService? tenantIsolationService = null)
        : base(options, tenantIsolationService)
    {
    }

    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();
    public DbSet<ManagementDatabaseConnection> ManagementDatabaseConnections => Set<ManagementDatabaseConnection>();
    public DbSet<TenantDefinition> TenantDefinitions => Set<TenantDefinition>();
    public DbSet<TenantSetting> TenantSettings => Set<TenantSetting>();
    public DbSet<TenantBackgroundJobState> TenantBackgroundJobStates => Set<TenantBackgroundJobState>();
    public DbSet<SavedViewDefinition> SavedViewDefinitions => Set<SavedViewDefinition>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ApplyTenantSchema(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(PlatformDbContext).Assembly,
            type => type.Namespace?.StartsWith("OfficeAutomation.Modules.Platform.", StringComparison.Ordinal) == true);
    }
}
