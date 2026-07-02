using Microsoft.AspNetCore.Mvc.Rendering;
using OfficeAutomation.Modules.Finance.Application;

namespace OfficeAutomation.Models;

public sealed class TrialBalancePageVM
{
    public Guid FiscalPeriodId { get; set; }
    public bool IncludeMoatagh { get; set; }
    public bool GroupByFloatingDetail { get; set; }
    public bool SixColumn { get; set; } = true;
    public List<SelectListItem> FiscalPeriodOptions { get; set; } = [];
    public TrialBalanceDto Report { get; set; } = new();
}
