using System.Text.Json.Serialization;

namespace OfficeAutomation.Services.Licensing;

/// <summary>
/// Strongly-typed license information parsed from a verified JWT license token.
/// </summary>
public sealed class LicenseInfo
{
    /// <summary>Unique identifier of the issued license.</summary>
    [JsonPropertyName("lic_id")]
    public Guid LicenseId { get; init; }

    /// <summary>Name of the licensee customer/company.</summary>
    [JsonPropertyName("customer_name")]
    public string CustomerName { get; init; } = string.Empty;

    /// <summary>Internal reference / ID for the customer.</summary>
    [JsonPropertyName("customer_id")]
    public string CustomerId { get; init; } = string.Empty;

    /// <summary>List of enabled modules (e.g. "Contracts", "Financial").</summary>
    [JsonPropertyName("modules")]
    public IReadOnlyList<string> Modules { get; init; } = Array.Empty<string>();

    /// <summary>Total allowed user accounts across the system.</summary>
    [JsonPropertyName("max_users")]
    public int MaxUsers { get; init; }

    private readonly IReadOnlyDictionary<string, int> _moduleUserLimits = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Optional user limits per module (e.g. {"Contracts": 20, "Financial": 10}).
    /// If a module is not present in this dictionary, it falls back to MaxUsers.
    /// </summary>
    [JsonPropertyName("module_user_limits")]
    public IReadOnlyDictionary<string, int> ModuleUserLimits
    {
        get => _moduleUserLimits;
        init => _moduleUserLimits = value != null
            ? new Dictionary<string, int>(value, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>SHA-256 target server hardware fingerprint hash.</summary>
    [JsonPropertyName("hardware_fingerprint")]
    public string HardwareFingerprint { get; init; } = string.Empty;

    /// <summary>License issuance timestamp (UTC).</summary>
    [JsonPropertyName("issued_at")]
    public DateTime IssuedAtUtc { get; init; }

    /// <summary>License expiration timestamp (UTC), or null if perpetual.</summary>
    [JsonPropertyName("expires_at")]
    public DateTime? ExpiresAtUtc { get; init; }

    /// <summary>License type (e.g. "trial", "subscription", "perpetual").</summary>
    [JsonPropertyName("license_type")]
    public string LicenseType { get; init; } = "subscription";

    /// <summary>Whether this license is perpetual (no expiry date).</summary>
    [JsonPropertyName("is_perpetual")]
    public bool IsPerpetual { get; init; }

    /// <summary>Number of remaining valid days. Null if perpetual; 0 if expired.</summary>
    public int? DaysRemaining
    {
        get
        {
            if (IsPerpetual || !ExpiresAtUtc.HasValue)
                return null;

            var remaining = (ExpiresAtUtc.Value - DateTime.UtcNow).TotalDays;
            return remaining > 0 ? (int)Math.Ceiling(remaining) : 0;
        }
    }

    /// <summary>
    /// Checks whether a given module is enabled under this license.
    /// </summary>
    public bool HasModule(string moduleName)
    {
        if (string.IsNullOrWhiteSpace(moduleName)) return false;
        return Modules.Any(m => string.Equals(m, moduleName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Returns the maximum users allowed for a specific module, falling back to global MaxUsers.
    /// </summary>
    public int GetMaxUsersForModule(string moduleName)
    {
        if (ModuleUserLimits.TryGetValue(moduleName, out var limit))
            return limit;

        return MaxUsers;
    }
}
