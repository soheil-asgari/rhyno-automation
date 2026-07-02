namespace OfficeAutomation.Modules.Finance.Domain;

public sealed class FiscalYear
{
    public int Id { get; set; }

    public string YearName { get; set; } = string.Empty;

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public bool IsClosed { get; set; }

    public List<FiscalPeriod> FiscalPeriods { get; set; } = [];

    public List<VoucherHeader> VoucherHeaders { get; set; } = [];
}
