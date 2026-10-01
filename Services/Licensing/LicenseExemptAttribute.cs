namespace OfficeAutomation.Services.Licensing;

/// <summary>
/// Marks a Controller or endpoint as explicitly exempt from module licensing checks.
/// These endpoints represent core infrastructure, authentication, license activation,
/// health checks, or basic platform features that are always accessible regardless of
/// the active customer license.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class LicenseExemptAttribute : Attribute
{
    public string? Reason { get; }

    public LicenseExemptAttribute(string? reason = null)
    {
        Reason = reason;
    }
}
