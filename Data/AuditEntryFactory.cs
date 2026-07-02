using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Services.Auditing;

namespace OfficeAutomation.Data;

public static class AuditEntryFactory
{
    public static List<PendingAuditLogEntry> Create(DbContext context, AuditRequestInfo requestInfo)
    {
        var auditEntries = new List<PendingAuditLogEntry>();

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.State is EntityState.Detached or EntityState.Unchanged)
            {
                continue;
            }

            if (entry.Entity is Models.AuditLog)
            {
                continue;
            }

            if (!AuditLogScope.IsAuditedEntity(entry.Metadata.ClrType))
            {
                continue;
            }

            var auditEntry = new PendingAuditLogEntry(entry, requestInfo);

            switch (entry.State)
            {
                case EntityState.Added:
                    auditEntry.Action = "Create";
                    PopulateAddedEntry(auditEntry);
                    break;
                case EntityState.Modified:
                    auditEntry.Action = "Update";
                    PopulateModifiedEntry(auditEntry);
                    break;
                case EntityState.Deleted:
                    auditEntry.Action = "Delete";
                    PopulateDeletedEntry(auditEntry);
                    break;
            }

            if (auditEntry.AffectedColumns.Count == 0 &&
                auditEntry.OldValues.Count == 0 &&
                auditEntry.NewValues.Count == 0)
            {
                continue;
            }

            auditEntries.Add(auditEntry);
        }

        return auditEntries;
    }

    private static void PopulateAddedEntry(PendingAuditLogEntry auditEntry)
    {
        foreach (var property in auditEntry.Entry.Properties)
        {
            if (property.IsTemporary)
            {
                auditEntry.TemporaryProperties.Add(property);
                continue;
            }

            auditEntry.AffectedColumns.Add(property.Metadata.Name);
            auditEntry.NewValues[property.Metadata.Name] = PendingAuditLogEntry.NormalizeValue(property.CurrentValue);
        }
    }

    private static void PopulateModifiedEntry(PendingAuditLogEntry auditEntry)
    {
        foreach (var property in auditEntry.Entry.Properties)
        {
            if (property.IsTemporary)
            {
                auditEntry.TemporaryProperties.Add(property);
                continue;
            }

            if (property.Metadata.IsPrimaryKey() || !property.IsModified || Equals(property.OriginalValue, property.CurrentValue))
            {
                continue;
            }

            auditEntry.AffectedColumns.Add(property.Metadata.Name);
            auditEntry.OldValues[property.Metadata.Name] = PendingAuditLogEntry.NormalizeValue(property.OriginalValue);
            auditEntry.NewValues[property.Metadata.Name] = PendingAuditLogEntry.NormalizeValue(property.CurrentValue);
        }
    }

    private static void PopulateDeletedEntry(PendingAuditLogEntry auditEntry)
    {
        foreach (var property in auditEntry.Entry.Properties)
        {
            auditEntry.AffectedColumns.Add(property.Metadata.Name);
            auditEntry.OldValues[property.Metadata.Name] = PendingAuditLogEntry.NormalizeValue(property.OriginalValue);
        }
    }
}
