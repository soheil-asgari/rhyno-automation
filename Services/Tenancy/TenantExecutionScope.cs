namespace OfficeAutomation.Services.Tenancy;

public interface ITenantExecutionScope
{
    IDisposable BeginScope(string tenantId);
}

public sealed class TenantExecutionScope : ITenantExecutionScope
{
    private readonly ICurrentTenantAccessor _currentTenantAccessor;
    private readonly ITenantRegistry _tenantRegistry;

    public TenantExecutionScope(ICurrentTenantAccessor currentTenantAccessor, ITenantRegistry tenantRegistry)
    {
        _currentTenantAccessor = currentTenantAccessor;
        _tenantRegistry = tenantRegistry;
    }

    public IDisposable BeginScope(string tenantId)
    {
        var previous = _currentTenantAccessor.Snapshot();
        var tenant = _tenantRegistry.GetTenant(tenantId);
        _currentTenantAccessor.Initialize(tenant);
        return new RevertScope(_currentTenantAccessor, previous);
    }

    private sealed class RevertScope : IDisposable
    {
        private readonly ICurrentTenantAccessor _currentTenantAccessor;
        private readonly Models.TenantDefinition? _previous;

        public RevertScope(ICurrentTenantAccessor currentTenantAccessor, Models.TenantDefinition? previous)
        {
            _currentTenantAccessor = currentTenantAccessor;
            _previous = previous;
        }

        public void Dispose()
        {
            if (_previous != null)
            {
                _currentTenantAccessor.Initialize(_previous);
            }
        }
    }
}
