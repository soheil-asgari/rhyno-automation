using System.Collections.Generic;

namespace OfficeAutomation.Models;

public sealed class OverviewDashboardViewModel
{
    public decimal TotalSales { get; set; }
    public decimal TotalPurchases { get; set; }
    public decimal AccountsReceivableBalance { get; set; }
    public decimal AccountsPayableBalance { get; set; }
    public List<BelowReorderProductDto> BelowReorderProducts { get; set; } = new();
    public List<PendingWorkflowDto> PendingWorkflows { get; set; } = new();
}

public sealed class BelowReorderProductDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal ReorderPoint { get; set; }
    public decimal CurrentStock { get; set; }
}

public sealed class PendingWorkflowDto
{
    public string Module { get; set; } = string.Empty;
    public int Count { get; set; }
}
