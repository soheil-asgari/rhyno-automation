using OfficeAutomation.Models;

namespace OfficeAutomation.Services.Tenancy;

public interface ITenantIsolationService
{
    TenantIsolationDescriptor GetCurrent();
    TenantIsolationDescriptor GetTenant(string tenantId);
}

public sealed class TenantIsolationService : ITenantIsolationService
{
    private readonly ICurrentTenantAccessor _currentTenantAccessor;
    private readonly ITenantRegistry _tenantRegistry;

    public TenantIsolationService(ICurrentTenantAccessor currentTenantAccessor, ITenantRegistry tenantRegistry)
    {
        _currentTenantAccessor = currentTenantAccessor;
        _tenantRegistry = tenantRegistry;
    }

    public TenantIsolationDescriptor GetCurrent()
    {
        var tenant = _currentTenantAccessor.IsInitialized && _currentTenantAccessor.Tenant is not null
            ? _currentTenantAccessor.Tenant
            : _tenantRegistry.GetDefaultTenant();

        return ToDescriptor(tenant);
    }

    public TenantIsolationDescriptor GetTenant(string tenantId)
    {
        return ToDescriptor(_tenantRegistry.GetTenant(tenantId));
    }

    private static TenantIsolationDescriptor ToDescriptor(TenantDefinition tenant)
    {
        return new TenantIsolationDescriptor(
            tenant.TenantId,
            tenant.IsolationMode,
            tenant.ConnectionString,
            tenant.DatabaseSchema,
            tenant.QueueNamespace,
            tenant.CachePrefix,
            tenant.StorageRoot,
            tenant.LogPrefix,
            tenant.LogRoot,
            tenant.SettingsNamespace,
            tenant.JobNamespace,
            tenant.RedisDatabase);
    }
}
