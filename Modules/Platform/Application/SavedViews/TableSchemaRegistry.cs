using System.Text.Json;

namespace OfficeAutomation.Modules.Platform.Application.SavedViews;

public interface ITableSchemaRegistry
{
    IReadOnlyCollection<SavedViewColumnDefinition> GetAllowedColumns(string targetGridId, IEnumerable<string> roles);
    string MaskColumnLayoutJson(string targetGridId, string columnLayoutJson, IEnumerable<string> roles);
}

public sealed class TableSchemaRegistry : ITableSchemaRegistry
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly Dictionary<string, IReadOnlyCollection<SavedViewColumnDefinition>> Schemas =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Finance_Vouchers"] =
            [
                new("voucherNumber", "شماره سند", "VoucherNumber", []),
                new("status", "وضعیت سند", "Status", []),
                new("voucherDate", "تاریخ سند", "VoucherDate", []),
                new("description", "شرح", "Description", []),
                new("totalAmount", "مبلغ کل", "TotalAmount", ["Finance.Amounts.View"]),
                new("journalType", "نوع ژورنال", "JournalType", []),
                new("floatingDetailCode", "کد تفصیلی شناور", "FloatingDetailAccount.Code", []),
                new("floatingDetailName", "نام تفصیلی شناور", "FloatingDetailAccount.Name", [])
            ],
            ["HR_Employees"] =
            [
                new("employeeNumber", "شماره پرسنلی", "EmployeeNumber", []),
                new("fullName", "نام و نام خانوادگی", "FullName", []),
                new("department", "واحد سازمانی", "Department", []),
                new("baseSalary", "حقوق پایه", "BaseSalary", ["HR.Compensation.View"])
            ]
        };

    public IReadOnlyCollection<SavedViewColumnDefinition> GetAllowedColumns(string targetGridId, IEnumerable<string> roles)
    {
        if (!Schemas.TryGetValue(targetGridId, out var columns))
        {
            return [];
        }

        var roleSet = new HashSet<string>(roles, StringComparer.OrdinalIgnoreCase);
        return columns
            .Where(column => column.RequiredRoles.Count == 0 || column.RequiredRoles.Any(roleSet.Contains))
            .ToList();
    }

    public string MaskColumnLayoutJson(string targetGridId, string columnLayoutJson, IEnumerable<string> roles)
    {
        var allowedColumnIds = GetAllowedColumns(targetGridId, roles)
            .Select(column => column.ColumnId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (allowedColumnIds.Count == 0)
        {
            return "[]";
        }

        var layout = JsonSerializer.Deserialize<List<SavedViewColumnLayout>>(columnLayoutJson, JsonOptions) ?? [];
        var masked = layout
            .Where(column => allowedColumnIds.Contains(column.ColumnId))
            .OrderBy(column => column.Order)
            .ToList();

        return JsonSerializer.Serialize(masked, JsonOptions);
    }
}
