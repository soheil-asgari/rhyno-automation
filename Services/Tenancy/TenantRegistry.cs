using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using OfficeAutomation.Modules.Platform.Infrastructure.Persistence;
using OfficeAutomation.Models;

namespace OfficeAutomation.Services.Tenancy;

public interface ITenantRegistry
{
    TenantDefinition GetDefaultTenant();
    Task<TenantDefinition> GetDefaultTenantAsync(CancellationToken cancellationToken = default);
    TenantDefinition GetTenant(string tenantId);
    Task<TenantDefinition> GetTenantAsync(string tenantId, CancellationToken cancellationToken = default);
    IReadOnlyList<TenantDefinition> GetAllTenants();
    Task<IReadOnlyList<TenantDefinition>> GetAllTenantsAsync(CancellationToken cancellationToken = default);
}

public sealed class TenantRegistry : ITenantRegistry
{
    private const string CacheKey = "tenant-registry:active-tenants";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(2);

    private readonly TenantOptions _options;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IMemoryCache _cache;

    public TenantRegistry(
        IOptions<TenantOptions> options,
        IServiceScopeFactory serviceScopeFactory,
        IMemoryCache cache)
    {
        _options = options.Value;
        _serviceScopeFactory = serviceScopeFactory;
        _cache = cache;
    }

    public TenantDefinition GetDefaultTenant()
    {
        return GetConfiguredTenant(_options.DefaultTenantId);
    }

    public Task<TenantDefinition> GetDefaultTenantAsync(CancellationToken cancellationToken = default)
    {
        return GetTenantAsync(_options.DefaultTenantId, cancellationToken);
    }

    public TenantDefinition GetTenant(string tenantId)
    {
        return GetConfiguredTenant(tenantId);
    }

    public async Task<TenantDefinition> GetTenantAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        var tenant = (await LoadActiveTenantsAsync(cancellationToken))
            .FirstOrDefault(item => string.Equals(item.TenantId, tenantId, StringComparison.OrdinalIgnoreCase));
        if (tenant == null)
        {
            throw new InvalidOperationException($"Tenant '{tenantId}' is not active in the tenant catalog.");
        }

        NormalizeTenant(tenant);
        return tenant;
    }

    public IReadOnlyList<TenantDefinition> GetAllTenants()
    {
        var tenants = LoadConfiguredTenants();
        foreach (var tenant in tenants)
        {
            NormalizeTenant(tenant);
        }

        return tenants;
    }

    public async Task<IReadOnlyList<TenantDefinition>> GetAllTenantsAsync(CancellationToken cancellationToken = default)
    {
        var tenants = await LoadActiveTenantsAsync(cancellationToken);
        foreach (var tenant in tenants)
        {
            NormalizeTenant(tenant);
        }

        return tenants;
    }

    private TenantDefinition GetConfiguredTenant(string tenantId)
    {
        var tenant = LoadConfiguredTenants()
            .FirstOrDefault(item => string.Equals(item.TenantId, tenantId, StringComparison.OrdinalIgnoreCase));
        if (tenant == null)
        {
            throw new InvalidOperationException($"Tenant '{tenantId}' is not configured.");
        }

        NormalizeTenant(tenant);
        return tenant;
    }

    private IReadOnlyList<TenantDefinition> LoadConfiguredTenants()
    {
        return _options.Tenants
            .Where(item => string.Equals(item.LifecycleState, TenantLifecycleState.Active, StringComparison.OrdinalIgnoreCase))
            .OrderBy(item => item.TenantId)
            .ToList();
    }

    private async Task<IReadOnlyList<TenantDefinition>> LoadActiveTenantsAsync(CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(CacheKey, out IReadOnlyList<TenantDefinition>? cachedTenants) && cachedTenants is not null)
        {
            return cachedTenants;
        }

        try
        {
            await using var scope = _serviceScopeFactory.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            var tenants = await context.TenantDefinitions
                .AsNoTracking()
                .Where(item => item.LifecycleState == TenantLifecycleState.Active)
                .OrderBy(item => item.TenantId)
                .ToListAsync(cancellationToken);

            if (tenants.Count > 0)
            {
                _cache.Set(CacheKey, tenants, CacheDuration);
                return tenants;
            }
        }
        catch (Exception)
        {
            _cache.Set(CacheKey, LoadConfiguredTenants(), TimeSpan.FromSeconds(15));
        }

        return LoadConfiguredTenants();
    }

    private static void NormalizeTenant(TenantDefinition tenant)
    {
        tenant.QueueNamespace = string.IsNullOrWhiteSpace(tenant.QueueNamespace) ? tenant.TenantId.ToLowerInvariant() : tenant.QueueNamespace.Trim();
        tenant.CachePrefix = string.IsNullOrWhiteSpace(tenant.CachePrefix) ? tenant.TenantId.ToLowerInvariant() : tenant.CachePrefix.Trim();
        tenant.StorageRoot = string.IsNullOrWhiteSpace(tenant.StorageRoot) ? Path.Combine("tenants", tenant.TenantId.ToLowerInvariant()) : tenant.StorageRoot.Trim();
        tenant.LogPrefix = string.IsNullOrWhiteSpace(tenant.LogPrefix) ? tenant.TenantId.ToLowerInvariant() : tenant.LogPrefix.Trim();
        tenant.DatabaseSchema = string.IsNullOrWhiteSpace(tenant.DatabaseSchema) ? "dbo" : tenant.DatabaseSchema.Trim();
        tenant.LogRoot = string.IsNullOrWhiteSpace(tenant.LogRoot) ? Path.Combine("logs", tenant.LogPrefix) : tenant.LogRoot.Trim();
        tenant.SettingsNamespace = string.IsNullOrWhiteSpace(tenant.SettingsNamespace) ? $"{tenant.TenantId.ToLowerInvariant()}:settings" : tenant.SettingsNamespace.Trim();
        tenant.JobNamespace = string.IsNullOrWhiteSpace(tenant.JobNamespace) ? $"{tenant.TenantId.ToLowerInvariant()}:jobs" : tenant.JobNamespace.Trim();
    }
}
