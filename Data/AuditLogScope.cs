using OfficeAutomation.Models;

namespace OfficeAutomation.Data
{
    internal static class AuditLogScope
    {
        private static readonly HashSet<Type> AuditedEntityTypes =
        [
            typeof(InsuranceList),
            typeof(InsuranceEmployee),
            typeof(Invoice),
            typeof(InvoiceItem),
            typeof(Waybill),
            typeof(Department),
            typeof(HumanCapitalEmployee),
            typeof(HumanCapitalSalaryHistory),
            typeof(HumanCapitalStatusHistory),
            typeof(Letter),
            typeof(Leave),
            typeof(PayrollList),
            typeof(PayrollItem),
            typeof(Warehouse),
            typeof(Product),
            typeof(WarehouseReceipt),
            typeof(WarehouseReceiptItem),
            typeof(WarehouseIssuance),
            typeof(WarehouseIssuanceItem),
            typeof(InventoryStock),
            typeof(InventoryCounting),
            typeof(InventoryCountingItem),
            typeof(WarehouseClosing),
            typeof(WarehouseClosingItem),
            typeof(InventoryOpeningBalanceLedger),
            typeof(Vendor),
            typeof(Employer),
            typeof(InventoryTransferRequest),
            typeof(WorkflowRoute)
        ];

        public static bool IsAuditedEntity(Type entityType)
        {
            return AuditedEntityTypes.Contains(entityType);
        }
    }
}
