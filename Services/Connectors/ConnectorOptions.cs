namespace OfficeAutomation.Services.Connectors;

public sealed class ConnectorOptions
{
    public const string SectionName = "Connectors";

    public int RetryCount { get; set; } = 3;
    public int RetryDelaySeconds { get; set; } = 2;
    public int CircuitBreakerFailures { get; set; } = 5;
    public int CircuitBreakerBreakSeconds { get; set; } = 30;
}
