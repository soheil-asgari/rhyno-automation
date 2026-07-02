using OfficeAutomation.Models;

namespace OfficeAutomation.Services.Tenancy;

public sealed class TenantOptions
{
    public const string SectionName = "Tenancy";

    public string DefaultTenantId { get; set; } = "default";
    public List<TenantDefinition> Tenants { get; set; } = [];
}
