namespace OfficeAutomation.Services.Licensing;

/// <summary>
/// Persisted state of the heartbeat client, stored in App_Data/license/heartbeat-state.json.
/// </summary>
public sealed class HeartbeatState
{
    public DateTime? LastSuccessfulHeartbeatAt { get; set; }
    public DateTime? FirstSeenAtUtc { get; set; }
    public DateTime? LastAttemptAtUtc { get; set; }
    public string? LastStatus { get; set; }
    public string? LastErrorMessage { get; set; }
    public Guid? LastLicenseId { get; set; }
}
