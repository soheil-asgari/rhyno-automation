using System.ComponentModel.DataAnnotations;

namespace OfficeAutomation.Models;

public static class TenantIsolationMode
{
    public const string DatabasePerTenant = "DatabasePerTenant";
    public const string SchemaPerTenant = "SchemaPerTenant";
    public const string DedicatedStackPerTenant = "DedicatedStackPerTenant";
}

public static class TenantLifecycleState
{
    public const string Provisioning = "Provisioning";
    public const string Active = "Active";
    public const string Suspended = "Suspended";
}

public class TenantDefinition
{
    [Required]
    [StringLength(64)]
    public string TenantId { get; set; } = string.Empty;

    [Required]
    [StringLength(128)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(40)]
    public string IsolationMode { get; set; } = TenantIsolationMode.DatabasePerTenant;

    [Required]
    public string ConnectionString { get; set; } = string.Empty;

    [Required]
    [StringLength(32)]
    public string LifecycleState { get; set; } = TenantLifecycleState.Active;

    [StringLength(64)]
    public string SchemaVersion { get; set; } = string.Empty;

    [StringLength(64)]
    public string Plan { get; set; } = "Standard";

    [StringLength(128)]
    public string DatabaseSchema { get; set; } = "dbo";

    [StringLength(128)]
    public string QueueNamespace { get; set; } = string.Empty;

    [StringLength(128)]
    public string CachePrefix { get; set; } = string.Empty;

    [StringLength(256)]
    public string StorageRoot { get; set; } = string.Empty;

    [StringLength(128)]
    public string LogPrefix { get; set; } = string.Empty;

    [StringLength(260)]
    public string LogRoot { get; set; } = string.Empty;

    [StringLength(128)]
    public string SettingsNamespace { get; set; } = string.Empty;

    [StringLength(128)]
    public string JobNamespace { get; set; } = string.Empty;

    public int? RedisDatabase { get; set; }

    public bool EnableOutboxPublisher { get; set; } = true;
    public bool EnableWorkflowJobs { get; set; } = true;

    public int RequestsPerMinute { get; set; } = 120;
    public int AiRequestsPerMinute { get; set; } = 20;
    public int? MaxActiveUsers { get; set; }
    public long? MaxStorageMegabytes { get; set; }
    public int? MaxWorkflowInstances { get; set; }
    public int? MaxStorageFiles { get; set; }
}
