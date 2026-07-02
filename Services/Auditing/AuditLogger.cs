using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OfficeAutomation.Data;
using OfficeAutomation.Models;
using OfficeAutomation.Services.Security;

namespace OfficeAutomation.Services.Auditing
{
    public sealed class AuditLogger : IAuditLogger
    {
        private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
        private readonly ICurrentDataAccessScope _dataAccessScope;

        public AuditLogger(ICurrentDataAccessScope dataAccessScope)
        {
            _dataAccessScope = dataAccessScope;
        }

        public AuditLog Create(PendingAuditLogEntry entry)
        {
            entry.FinalizeTemporaryProperties();

            var changes = entry.AffectedColumns
                .Select(column => new AuditFieldChange(
                    column,
                    entry.OldValues.TryGetValue(column, out var oldValue) ? oldValue : null,
                    entry.NewValues.TryGetValue(column, out var newValue) ? newValue : null))
                .ToArray();

            var changeSet = new AuditChangeSet(entry.AffectedColumns.ToArray(), changes);
            var userContext = new AuditUserContext(
                entry.RequestInfo.UserId,
                entry.RequestInfo.UserName,
                entry.RequestInfo.DisplayName,
                _dataAccessScope.DepartmentId,
                entry.RequestInfo.Roles,
                entry.RequestInfo.Permissions,
                entry.RequestInfo.UserIP,
                entry.RequestInfo.UserAgent);

            var oldValuesJson = entry.OldValues.Count == 0 ? null : JsonSerializer.Serialize(entry.OldValues, SerializerOptions);
            var newValuesJson = entry.NewValues.Count == 0 ? null : JsonSerializer.Serialize(entry.NewValues, SerializerOptions);
            var affectedColumnsJson = entry.AffectedColumns.Count == 0 ? null : JsonSerializer.Serialize(entry.AffectedColumns, SerializerOptions);
            var userContextJson = JsonSerializer.Serialize(userContext, SerializerOptions);
            var changeSetJson = JsonSerializer.Serialize(changeSet, SerializerOptions);
            var severity = IsSensitive(entry) ? "high" : "informational";
            var complianceCategory = ResolveComplianceCategory(entry);
            var structuredPayload = JsonSerializer.Serialize(new SiemAuditLogDto
            {
                EventId = Guid.NewGuid(),
                OccurredAtUtc = DateTimeOffset.UtcNow,
                Severity = severity,
                Module = ResolveModule(entry.TableName),
                Action = entry.Action,
                TableName = entry.TableName,
                EntityId = entry.EntityId,
                TenantId = _dataAccessScope.TenantId,
                CorrelationId = entry.RequestInfo.CorrelationId,
                IsSensitive = IsSensitive(entry),
                IntegrityHash = ComputeHash(entry, userContextJson, changeSetJson),
                Actor = new SiemActorDto
                {
                    UserId = entry.RequestInfo.UserId,
                    DisplayName = entry.RequestInfo.DisplayName,
                    Roles = entry.RequestInfo.Roles,
                    Permissions = entry.RequestInfo.Permissions
                },
                Device = new SiemDeviceDto
                {
                    IpAddress = entry.RequestInfo.UserIP,
                    UserAgent = entry.RequestInfo.UserAgent
                },
                UserContext = JsonSerializer.Deserialize<object>(userContextJson, SerializerOptions),
                ChangeSet = JsonSerializer.Deserialize<object>(changeSetJson, SerializerOptions),
                ComplianceTags = BuildComplianceTags(entry, entry.RequestInfo.Clearance)
            }, SerializerOptions);

            return new AuditLog
            {
                Id = Guid.NewGuid(),
                TenantId = _dataAccessScope.TenantId,
                UserId = entry.RequestInfo.UserId,
                Action = entry.Action,
                TableName = entry.TableName,
                EntityId = entry.EntityId ?? string.Empty,
                CorrelationId = entry.RequestInfo.CorrelationId,
                DateTime = DateTimeOffset.UtcNow,
                OldValues = oldValuesJson,
                NewValues = newValuesJson,
                AffectedColumns = affectedColumnsJson,
                UserIP = entry.RequestInfo.UserIP,
                UserAgent = entry.RequestInfo.UserAgent,
                IsSensitive = IsSensitive(entry),
                UserContext = userContextJson,
                ChangeSet = changeSetJson,
                Severity = severity,
                ComplianceCategory = complianceCategory,
                StructuredPayload = structuredPayload,
                IntegrityHash = ComputeHash(entry, userContextJson, changeSetJson)
            };
        }

        private static bool IsSensitive(PendingAuditLogEntry entry)
        {
            return string.Equals(entry.Action, "Delete", StringComparison.OrdinalIgnoreCase) ||
                   entry.TableName.Contains("Permission", StringComparison.OrdinalIgnoreCase) ||
                   entry.TableName.Contains("Role", StringComparison.OrdinalIgnoreCase) ||
                   entry.TableName.Contains("User", StringComparison.OrdinalIgnoreCase);
        }

        private static string ComputeHash(PendingAuditLogEntry entry, string userContextJson, string changeSetJson)
        {
            var payload = string.Join("|",
                entry.RequestInfo.CorrelationId ?? string.Empty,
                entry.RequestInfo.UserId ?? string.Empty,
                entry.Action,
                entry.TableName,
                entry.EntityId ?? string.Empty,
                userContextJson,
                changeSetJson);

            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
            return Convert.ToHexString(bytes);
        }

        private static string ResolveComplianceCategory(PendingAuditLogEntry entry)
        {
            if (entry.TableName.Contains("HumanCapital", StringComparison.OrdinalIgnoreCase) ||
                entry.TableName.Contains("User", StringComparison.OrdinalIgnoreCase))
            {
                return "PII";
            }

            if (entry.TableName.Contains("Invoice", StringComparison.OrdinalIgnoreCase) ||
                entry.TableName.Contains("Payroll", StringComparison.OrdinalIgnoreCase))
            {
                return "Financial";
            }

            return "Operational";
        }

        private static string ResolveModule(string tableName)
        {
            if (tableName.Contains("Invoice", StringComparison.OrdinalIgnoreCase) || tableName.Contains("Payroll", StringComparison.OrdinalIgnoreCase))
            {
                return "Finance";
            }

            if (tableName.Contains("HumanCapital", StringComparison.OrdinalIgnoreCase) || tableName.Contains("Leave", StringComparison.OrdinalIgnoreCase))
            {
                return "HR";
            }

            if (tableName.Contains("Role", StringComparison.OrdinalIgnoreCase) || tableName.Contains("Permission", StringComparison.OrdinalIgnoreCase) || tableName.Contains("User", StringComparison.OrdinalIgnoreCase))
            {
                return "Security";
            }

            return "General";
        }

        private static IReadOnlyList<string> BuildComplianceTags(PendingAuditLogEntry entry, string? clearance)
        {
            var tags = new List<string> { "audit", "siem-ready", ResolveComplianceCategory(entry).ToLowerInvariant() };
            if (IsSensitive(entry))
            {
                tags.Add("sensitive");
            }

            if (!string.IsNullOrWhiteSpace(clearance))
            {
                tags.Add("clearance:" + clearance.ToLowerInvariant());
            }

            return tags;
        }
    }
}
