namespace OfficeAutomation.Services.Outbox;

public sealed class OutboxOptions
{
    public const string SectionName = "Outbox";

    public int BatchSize { get; set; } = 20;
    public int PollIntervalSeconds { get; set; } = 10;
    public int LockTimeoutSeconds { get; set; } = 60;
    public int MaxRetryCount { get; set; } = 15;
}
