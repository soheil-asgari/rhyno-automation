using OfficeAutomation.Models;

namespace OfficeAutomation.Services.Tenancy;

public interface ICurrentTenantAccessor
{
    TenantDefinition? Tenant { get; }
    string? TenantId { get; }
    bool IsInitialized { get; }
    void Initialize(TenantDefinition tenant);
    TenantDefinition? Snapshot();
}

public sealed class CurrentTenantAccessor : ICurrentTenantAccessor
{
    public TenantDefinition? Tenant { get; private set; }
    public string? TenantId => Tenant?.TenantId;
    public bool IsInitialized { get; private set; }

    public void Initialize(TenantDefinition tenant)
    {
        Tenant = tenant;
        IsInitialized = true;
    }

    public TenantDefinition? Snapshot()
    {
        return Tenant;
    }
}
