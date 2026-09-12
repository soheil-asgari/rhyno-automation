namespace LicenseIssuer;

public sealed class LicenseIssueRequest
{
    public Guid LicenseId { get; set; } = Guid.NewGuid();
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public List<string> Modules { get; set; } = new();
    public int MaxUsers { get; set; } = 10;
    public Dictionary<string, int> ModuleUserLimits { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string HardwareFingerprint { get; set; } = string.Empty;
    public DateTime IssuedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAtUtc { get; set; }
    public string LicenseType { get; set; } = "subscription"; // "trial", "subscription", "perpetual"
    public bool IsPerpetual { get; set; } = false;
}
