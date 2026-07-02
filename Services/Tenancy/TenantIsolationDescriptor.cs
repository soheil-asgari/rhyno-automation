using OfficeAutomation.Models;

namespace OfficeAutomation.Services.Tenancy;

public sealed record TenantIsolationDescriptor(
    string TenantId,
    string IsolationMode,
    string ConnectionString,
    string DatabaseSchema,
    string QueueNamespace,
    string CachePrefix,
    string StorageRoot,
    string LogPrefix,
    string LogRoot,
    string SettingsNamespace,
    string JobNamespace,
    int? RedisDatabase);
