namespace OfficeAutomation.Modules.Finance.Domain;

public sealed class FiscalPeriod
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public int FiscalYearId { get; set; }

    public FiscalYear FiscalYear { get; set; } = null!;

    public string Name { get; set; } = string.Empty;

    public int PeriodNumber { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public string Status { get; set; } = FiscalPeriodStatus.Open;
}
