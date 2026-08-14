namespace OfficeAutomation.Modules.Finance.Domain;

/// <summary>
/// Records every fiscal period status change, especially reopenings from Closed/Locked to Open.
/// </summary>
public sealed class FiscalPeriodAuditLog
{
    public long Id { get; set; }

    public Guid FiscalPeriodId { get; set; }

    public FiscalPeriod? FiscalPeriod { get; set; }

    public int? FiscalYearId { get; set; }

    public string UserId { get; set; } = string.Empty;

    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    public string PreviousStatus { get; set; } = string.Empty;

    public string NewStatus { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;
}