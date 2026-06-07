using OfficeAutomation.Models;

namespace OfficeAutomation.Services.Security
{
    public static class PermissionCatalog
    {
        public static readonly IReadOnlyList<Permission> CorePermissions =
        [
            new() { Key = "Letters.Read", DisplayName = "Read letters", Category = "Letters", Description = "View letters and inbox items." },
            new() { Key = "Letters.Create", DisplayName = "Create letters", Category = "Letters", Description = "Create and route new letters." },
            new() { Key = "Letters.Delete", DisplayName = "Delete letters", Category = "Letters", Description = "Delete letters." },
            new() { Key = "HR.View", DisplayName = "View HR", Category = "HR", Description = "View HR employee records." },
            new() { Key = "HR.Approve", DisplayName = "Approve HR", Category = "HR", Description = "Approve HR workflows and actions." },
            new() { Key = "Finance.View", DisplayName = "View finance", Category = "Finance", Description = "View finance dashboards and records." },
            new() { Key = "Finance.Delete", DisplayName = "Delete finance", Category = "Finance", Description = "Delete finance records." },
            new() { Key = "Warehouse.View", DisplayName = "View warehouse", Category = "Warehouse", Description = "View warehouse operations." },
            new() { Key = "Warehouse.Approve", DisplayName = "Approve warehouse", Category = "Warehouse", Description = "Approve warehouse actions." },
            new() { Key = "Users.Manage", DisplayName = "Manage users", Category = "Administration", Description = "Create and maintain users." },
            new() { Key = "Roles.Manage", DisplayName = "Manage roles", Category = "Administration", Description = "Create and maintain roles." },
            new() { Key = "Permissions.Manage", DisplayName = "Manage permissions", Category = "Administration", Description = "Assign permissions to roles." },
            new() { Key = "SystemSettings.View", DisplayName = "View settings", Category = "Settings", Description = "View system settings." },
            new() { Key = "SystemSettings.Manage", DisplayName = "Manage settings", Category = "Settings", Description = "Update system settings." },
            new() { Key = "Security.Manage", DisplayName = "Manage security", Category = "Security", Description = "Manage RBAC, permissions, and access rules." },
            new() { Key = "AuditLogs.Read", DisplayName = "Read audit logs", Category = "Security", Description = "View audit logs." }
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
                ["AuditLogs"] = ["AuditLogs.Read"]
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
