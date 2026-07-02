using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using OfficeAutomation.Services.Auditing;

namespace OfficeAutomation.Data
{
    public sealed class PendingAuditLogEntry
    {
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
        public string? EntityId { get; private set; }
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

            EntityId ??= ResolveEntityId();
        }

        private string? ResolveEntityId()
        {
            var key = Entry.Metadata.FindPrimaryKey();
            if (key == null || key.Properties.Count == 0)
            {
                return null;
            }

            var parts = new List<string>(key.Properties.Count);
            foreach (var keyProperty in key.Properties)
            {
                var propertyEntry = Entry.Property(keyProperty.Name);
                var value = propertyEntry.CurrentValue ?? propertyEntry.OriginalValue;
                if (value == null)
                {
                    continue;
                }

                parts.Add(Convert.ToString(NormalizeValue(value), System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty);
            }

            return parts.Count == 0 ? null : string.Join("|", parts);
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
