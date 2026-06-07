using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using OfficeAutomation.Models;
using OfficeAutomation.Services.Auditing;

namespace OfficeAutomation.Data
{
    internal sealed class PendingAuditLogEntry
    {
        private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

        public PendingAuditLogEntry(EntityEntry entry, AuditRequestInfo requestInfo)
        {
            Entry = entry;
            RequestInfo = requestInfo;
            TableName = entry.Metadata.GetTableName() ?? entry.Metadata.ClrType.Name;
        }

        public EntityEntry Entry { get; }
        public AuditRequestInfo RequestInfo { get; }
        public string Action { get; set; } = string.Empty;
        public string TableName { get; }
        public Dictionary<string, object?> OldValues { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, object?> NewValues { get; } = new(StringComparer.Ordinal);
        public List<string> AffectedColumns { get; } = [];
        public List<PropertyEntry> TemporaryProperties { get; } = [];

        public bool HasTemporaryProperties => TemporaryProperties.Count > 0;

        public void FinalizeTemporaryProperties()
        {
            foreach (var property in TemporaryProperties)
            {
                if (property.Metadata.IsPrimaryKey())
                {
                    NewValues[property.Metadata.Name] = NormalizeValue(property.CurrentValue);
                    if (!AffectedColumns.Contains(property.Metadata.Name, StringComparer.Ordinal))
                    {
                        AffectedColumns.Add(property.Metadata.Name);
                    }
                }
            }
        }

        public AuditLog ToAuditLog()
        {
            return new AuditLog
            {
                UserId = RequestInfo.UserId,
                Action = Action,
                TableName = TableName,
                DateTime = DateTimeOffset.UtcNow,
                OldValues = OldValues.Count == 0 ? null : JsonSerializer.Serialize(OldValues, SerializerOptions),
                NewValues = NewValues.Count == 0 ? null : JsonSerializer.Serialize(NewValues, SerializerOptions),
                AffectedColumns = AffectedColumns.Count == 0 ? null : JsonSerializer.Serialize(AffectedColumns, SerializerOptions),
                UserIP = RequestInfo.UserIP,
                UserAgent = RequestInfo.UserAgent
            };
        }

        public static object? NormalizeValue(object? value)
        {
            if (value is null)
            {
                return null;
            }

            return value switch
            {
                byte[] bytes => Convert.ToBase64String(bytes),
                DateTime dateTime => dateTime.Kind == DateTimeKind.Unspecified
                    ? DateTime.SpecifyKind(dateTime, DateTimeKind.Utc)
                    : dateTime,
                DateOnly dateOnly => dateOnly.ToString("O"),
                TimeOnly timeOnly => timeOnly.ToString("O"),
                _ => value
            };
        }
    }
}
