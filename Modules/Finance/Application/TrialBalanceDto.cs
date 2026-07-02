namespace OfficeAutomation.Modules.Finance.Application;

public sealed class TrialBalanceDto
{
    public Guid FiscalPeriodId { get; set; }
    public string FiscalPeriodName { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IncludeMoatagh { get; set; }
    public bool GroupByFloatingDetail { get; set; }
    public IReadOnlyList<TrialBalanceRowDto> Rows { get; set; } = [];
    public decimal OpeningDebitTotal { get; set; }
    public decimal OpeningCreditTotal { get; set; }
    public decimal PeriodDebitTotal { get; set; }
    public decimal PeriodCreditTotal { get; set; }
    public decimal ClosingDebitTotal { get; set; }
    public decimal ClosingCreditTotal { get; set; }
}

public sealed class TrialBalanceRowDto
{
    public string AccountCode { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string? FloatingDetailCode { get; set; }
    public string? FloatingDetailName { get; set; }
    public decimal OpeningDebit { get; set; }
    public decimal OpeningCredit { get; set; }
    public decimal PeriodDebit { get; set; }
    public decimal PeriodCredit { get; set; }
    public decimal ClosingDebit { get; set; }
    public decimal ClosingCredit { get; set; }
}

public sealed record TrialBalanceRequest(
    Guid FiscalPeriodId,
    bool IncludeMoatagh,
    bool GroupByFloatingDetail);
