namespace OfficeAutomation.Modules.TaxAnalytics;

/// <summary>Severity of a generated tax insight / anomaly alert.</summary>
public enum InsightSeverity
{
    Info = 0,
    Warning = 1,
    Critical = 2
}

/// <summary>A single automated anomaly / trend alert.</summary>
public sealed class TaxInsightAlert
{
    public string Code { get; set; } = string.Empty;
    public InsightSeverity Severity { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;

    /// <summary>Optional affected metric value.</summary>
    public decimal? MetricValue { get; set; }

    /// <summary>Optional reference period (e.g. "1403/05").</summary>
    public string? Period { get; set; }
}

/// <summary>Aggregate snapshot used to build the tax analytics dashboard.</summary>
public sealed class TaxAnalyticsSnapshot
{
    public int TotalInvoices { get; set; }
    public int MissingEconomicCodeInvoices { get; set; }
    public decimal TotalTaxPaid { get; set; }
    public decimal PreviousMonthTaxPaid { get; set; }
    public decimal CurrentMonthTaxPaid { get; set; }
    public decimal? TaxMoMChangePercent { get; set; }
    public int PendingSubmissions { get; set; }
    public int RejectedSubmissions { get; set; }
    public List<TaxInsightAlert> Alerts { get; set; } = new();
}

/// <summary>Request body for the AI tax assistant query endpoint.</summary>
public sealed class TaxAssistantQuestion
{
    public string Question { get; set; } = string.Empty;
}