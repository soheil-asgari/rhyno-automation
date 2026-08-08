using OfficeAutomation.Modules.Persistence.Entities;

namespace OfficeAutomation.Modules.TaxAnalytics;

/// <summary>
/// Automated anomaly detection over historical tax data.
/// </summary>
public interface ITaxInsightsService
{
    /// <summary>Builds a full analytics snapshot with generated alerts.</summary>
    Task<TaxAnalyticsSnapshot> BuildSnapshotAsync(CancellationToken cancellationToken);

    /// <summary>Generates only the anomaly alerts for the current period.</summary>
    Task<IReadOnlyList<TaxInsightAlert>> GenerateAlertsAsync(CancellationToken cancellationToken);
}

/// <summary>
/// A single line item used by the analytics snapshot for trend computation.
/// </summary>
public sealed class TaxPeriodAmount
{
    public string Period { get; set; } = string.Empty;
    public decimal TaxAmount { get; set; }
    public int InvoiceCount { get; set; }
    public int MissingEconomicCode { get; set; }
}