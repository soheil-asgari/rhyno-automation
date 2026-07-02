using Microsoft.AspNetCore.Http;

namespace OfficeAutomation.Services.Tenancy;

public interface ITenantResolver
{
    string ResolveTenantId();
    Task<string> ResolveTenantIdAsync(CancellationToken cancellationToken = default);
}

public sealed class TenantResolver : ITenantResolver
{
    private const string HeaderName = "X-Tenant-ID";
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ITenantRegistry _tenantRegistry;

    public TenantResolver(IHttpContextAccessor httpContextAccessor, ITenantRegistry tenantRegistry)
    {
        _httpContextAccessor = httpContextAccessor;
        _tenantRegistry = tenantRegistry;
    }

    public string ResolveTenantId()
    {
        var context = _httpContextAccessor.HttpContext;
        var tenantId = ResolveTenantIdFromRequest(context);
        return tenantId ?? _tenantRegistry.GetDefaultTenant().TenantId;
    }

    public async Task<string> ResolveTenantIdAsync(CancellationToken cancellationToken = default)
    {
        var context = _httpContextAccessor.HttpContext;
        var tenantId = ResolveTenantIdFromRequest(context);
        return tenantId ?? (await _tenantRegistry.GetDefaultTenantAsync(cancellationToken)).TenantId;
    }

    private static string? ResolveTenantIdFromRequest(HttpContext? context)
    {
        if (context == null)
        {
            return null;
        }

        if (context.Request.Headers.TryGetValue(HeaderName, out var values) && !string.IsNullOrWhiteSpace(values.FirstOrDefault()))
        {
            return values.First();
        }

        if (context.Request.Query.TryGetValue("tenant", out var queryValues) && !string.IsNullOrWhiteSpace(queryValues.FirstOrDefault()))
        {
            return queryValues.First();
        }

        return null;
    }
}
