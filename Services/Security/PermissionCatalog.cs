using OfficeAutomation.Models;

namespace OfficeAutomation.Services.Security
{
    public static class PermissionCatalog
    {
        public static readonly IReadOnlyList<Permission> CorePermissions =
        [
            new() { Key = "Letters.Read", DisplayName = "Read letters", Category = "Letters", Description = "View letters and inbox items." },
            new() { Key = "Letters.Create", DisplayName = "Create letters", Category = "Letters", Description = "Create and route new letters." },
            new() { Key = "Letters.Edit", DisplayName = "Edit letters", Category = "Letters", Description = "Edit existing letters." },
            new() { Key = "Letters.Delete", DisplayName = "Delete letters", Category = "Letters", Description = "Delete letters." },
            new() { Key = "Letters.Approve", DisplayName = "Approve letters", Category = "Letters", Description = "Approve letter workflow actions." },
            new() { Key = "Letters.Export", DisplayName = "Export letters", Category = "Letters", Description = "Export letters reports." },
            new() { Key = "Letters.ViewSensitive", DisplayName = "View confidential letters", Category = "Letters", Description = "View confidential letter content." },
            new() { Key = "HR.View", DisplayName = "View HR", Category = "HR", Description = "View HR employee records." },
            new() { Key = "HR.Create", DisplayName = "Create HR records", Category = "HR", Description = "Create HR records." },
            new() { Key = "HR.Edit", DisplayName = "Edit HR records", Category = "HR", Description = "Edit HR records." },
            new() { Key = "HR.Delete", DisplayName = "Delete HR records", Category = "HR", Description = "Delete HR records." },
            new() { Key = "HR.Approve", DisplayName = "Approve HR", Category = "HR", Description = "Approve HR workflows and actions." },
            new() { Key = "HR.Export", DisplayName = "Export HR", Category = "HR", Description = "Export HR reports." },
            new() { Key = "HR.ViewSensitive", DisplayName = "View confidential HR data", Category = "HR", Description = "View salary and confidential HR fields." },
            new() { Key = "Finance.View", DisplayName = "View finance", Category = "Finance", Description = "View finance dashboards and records." },
            new() { Key = "Finance.Create", DisplayName = "Create finance records", Category = "Finance", Description = "Create invoices and finance records." },
            new() { Key = "Finance.Edit", DisplayName = "Edit finance records", Category = "Finance", Description = "Edit finance records." },
            new() { Key = "Finance.Delete", DisplayName = "Delete finance", Category = "Finance", Description = "Delete finance records." },
            new() { Key = "Finance.Approve", DisplayName = "Approve finance", Category = "Finance", Description = "Approve finance operations." },
            new() { Key = "Finance.Export", DisplayName = "Export finance", Category = "Finance", Description = "Export finance reports." },
            new() { Key = "Finance.ViewSensitive", DisplayName = "View finance amounts", Category = "Finance", Description = "View sensitive finance amounts and totals." },
            new() { Key = "Warehouse.View", DisplayName = "View warehouse", Category = "Warehouse", Description = "View warehouse operations." },
            new() { Key = "Warehouse.Create", DisplayName = "Create warehouse records", Category = "Warehouse", Description = "Create warehouse records." },
            new() { Key = "Warehouse.Edit", DisplayName = "Edit warehouse records", Category = "Warehouse", Description = "Edit warehouse records." },
            new() { Key = "Warehouse.Delete", DisplayName = "Delete warehouse records", Category = "Warehouse", Description = "Delete warehouse records." },
            new() { Key = "Warehouse.Approve", DisplayName = "Approve warehouse", Category = "Warehouse", Description = "Approve warehouse actions." },
            new() { Key = "Warehouse.Export", DisplayName = "Export warehouse", Category = "Warehouse", Description = "Export warehouse reports." },
            new() { Key = "Warehouse.ViewSensitive", DisplayName = "View confidential warehouse values", Category = "Warehouse", Description = "View sensitive inventory valuations." },
            new() { Key = "Users.Manage", DisplayName = "Manage users", Category = "Administration", Description = "Create and maintain users." },
            new() { Key = "Roles.Manage", DisplayName = "Manage roles", Category = "Administration", Description = "Create and maintain roles." },
            new() { Key = "Permissions.Manage", DisplayName = "Manage permissions", Category = "Administration", Description = "Assign permissions to roles." },
            new() { Key = "Calendar.View", DisplayName = "View organization calendar", Category = "Calendar", Description = "View unified organization calendar." },
            new() { Key = "Calendar.Create", DisplayName = "Create calendar events", Category = "Calendar", Description = "Create organization calendar events." },
            new() { Key = "Archive.View", DisplayName = "View document archive", Category = "Archive", Description = "View archived files and attachments." },
            new() { Key = "Archive.Create", DisplayName = "Upload archive documents", Category = "Archive", Description = "Upload and archive files." },
            new() { Key = "Archive.ViewSensitive", DisplayName = "View restricted archive documents", Category = "Archive", Description = "View restricted archive files." },
            new() { Key = "SystemSettings.View", DisplayName = "View settings", Category = "Settings", Description = "View system settings." },
            new() { Key = "SystemSettings.Manage", DisplayName = "Manage settings", Category = "Settings", Description = "Update system settings." },
            new() { Key = "Security.Manage", DisplayName = "Manage security", Category = "Security", Description = "Manage RBAC, permissions, and access rules." },
            new() { Key = "AuditLogs.Read", DisplayName = "Read audit logs", Category = "Security", Description = "View audit logs." },
            new() { Key = "AuditLogs.Export", DisplayName = "Export audit logs", Category = "Security", Description = "Export audit logs." }
        ];

        public static readonly IReadOnlyDictionary<string, string[]> ControllerFallbackPermissions =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["Financial"] = ["Finance.View"],
                ["Payroll"] = ["Finance.View"],
                ["Bimeh"] = ["Finance.View"],
                ["Warehouse"] = ["Warehouse.View"],
                ["Vendors"] = ["Warehouse.View"],
                ["Employers"] = ["Warehouse.View"],
                ["HumanCapital"] = ["HR.View"],
                ["Settings"] = ["SystemSettings.View"],
                ["Security"] = ["Security.Manage"],
                ["Letters"] = ["Letters.Read"],
                ["Users"] = ["Users.Manage"],
                ["AuditLogs"] = ["AuditLogs.Read"],
                ["OrganizationCalendar"] = ["Calendar.View"],
                ["DocumentArchive"] = ["Archive.View"]
            };

        public static readonly IReadOnlyDictionary<string, string[]> LegacyPermissionAliases =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["Finance"] = ["Finance.View"],
                ["Warehouse"] = ["Warehouse.View"],
                ["HumanCapital"] = ["HR.View"],
                ["SystemSettings"] = ["SystemSettings.View", "SystemSettings.Manage"],
                ["WorkflowAdministration"] = ["Security.Manage", "AuditLogs.Read"]
            };
    }
}
