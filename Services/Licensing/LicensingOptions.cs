namespace OfficeAutomation.Services.Licensing;

public sealed class LicensingOptions
{
    public const string SectionName = "Licensing";

    /// <summary>
    /// License server endpoint for heartbeat POST requests.
    /// Default is empty / unset until the Python License Server is deployed.
    /// </summary>
    public string? HeartbeatUrl { get; set; }

    /// <summary>
    /// Optional API Key header for authenticating with the license server.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Number of days before the license locks when heartbeat calls cannot reach the server.
    /// Default is 21 days.
    /// </summary>
    public int HeartbeatGraceDays { get; set; } = 21;

    /// <summary>
    /// Interval in hours between scheduled heartbeat checks.
    /// Default is 24 hours.
    /// </summary>
    public int HeartbeatIntervalHours { get; set; } = 24;

    /// <summary>
    /// Initial startup delay in seconds before performing the first heartbeat check.
    /// Default is 30 seconds.
    /// </summary>
    public int InitialStartupDelaySeconds { get; set; } = 30;
}
