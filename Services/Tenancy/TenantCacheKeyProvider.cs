namespace OfficeAutomation.Services.Tenancy;

public interface ITenantCacheKeyProvider
{
    string Prefix(string key);
}

public sealed class TenantCacheKeyProvider : ITenantCacheKeyProvider
{
    private readonly ITenantIsolationService _tenantIsolationService;

    public TenantCacheKeyProvider(ITenantIsolationService tenantIsolationService)
    {
        _tenantIsolationService = tenantIsolationService;
    }

    public string Prefix(string key)
    {
        return $"{_tenantIsolationService.GetCurrent().CachePrefix}:{key}";
    }
}
