namespace OfficeAutomation.Modules.TaxAnalytics;

/// <summary>Result of interpreting a natural-language tax query.</summary>
public sealed class TaxQueryResult
{
    public bool Success { get; set; }
    public string Answer { get; set; } = string.Empty;

    /// <summary>Structured data points backing the answer.</summary>
    public List<TaxQueryDataPoint> DataPoints { get; set; } = new();
}

/// <summary>A single structured data point returned to the assistant UI.</summary>
public sealed class TaxQueryDataPoint
{
    public string Label { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string? Unit { get; set; }
}

/// <summary>
/// Interprets simple Persian natural-language questions against the persisted invoice,
/// tax and seller/buyer data to power the AI chat assistant without relying on the LLM
/// for every query (fast, deterministic, and safe).
/// </summary>
public interface ITaxQueryService
{
    Task<TaxQueryResult> AnswerAsync(string question, CancellationToken cancellationToken);
}