namespace OfficeAutomation.Services.Tenancy;

public interface ITenantQueueNameResolver
{
    string ResolveExchangeName(string exchangeName);
    string ResolveRoutingKey(string routingKey);
}

public sealed class TenantQueueNameResolver : ITenantQueueNameResolver
{
    private readonly ITenantIsolationService _tenantIsolationService;

    public TenantQueueNameResolver(ITenantIsolationService tenantIsolationService)
    {
        _tenantIsolationService = tenantIsolationService;
    }

    public string ResolveExchangeName(string exchangeName)
    {
        return $"{_tenantIsolationService.GetCurrent().QueueNamespace}.{exchangeName}";
    }

    public string ResolveRoutingKey(string routingKey)
    {
        return $"{_tenantIsolationService.GetCurrent().QueueNamespace}.{routingKey}";
    }
}
