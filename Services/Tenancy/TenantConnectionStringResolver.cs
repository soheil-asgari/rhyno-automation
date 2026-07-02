namespace OfficeAutomation.Services.Tenancy;

public interface ITenantConnectionStringResolver
{
    string GetConnectionString();
}

public sealed class TenantConnectionStringResolver : ITenantConnectionStringResolver
{
    private readonly ITenantIsolationService _tenantIsolationService;

    public TenantConnectionStringResolver(ITenantIsolationService tenantIsolationService)
    {
        _tenantIsolationService = tenantIsolationService;
    }

    public string GetConnectionString()
    {
        return _tenantIsolationService.GetCurrent().ConnectionString;
    }
}
