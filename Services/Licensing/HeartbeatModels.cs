using System.Text.Json.Serialization;

namespace OfficeAutomation.Services.Licensing;

public sealed class HeartbeatRequest
{
    [JsonPropertyName("lic_id")]
    public Guid LicenseId { get; set; }

    [JsonPropertyName("fingerprint")]
    public string Fingerprint { get; set; } = string.Empty;

    [JsonPropertyName("current_user_counts")]
    public Dictionary<string, int>? CurrentUserCounts { get; set; }
}

public sealed class HeartbeatResponse
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty; // "active" | "revoked" | "updated"

    [JsonPropertyName("updatedLicenseFile")]
    public string? UpdatedLicenseFile { get; set; }
}

public enum GracePeriodStatus
{
    NotApplicable = 0,
    Normal = 1,
    WarningAmber = 2, // <= 7 days
    WarningRed = 3,   // <= 2 days
    Expired = 4       // <= 0 days (Locked)
}
