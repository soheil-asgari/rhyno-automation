namespace OfficeAutomation.Services.Licensing;

/// <summary>
/// Detailed result of validating a license token.
/// </summary>
public sealed class LicenseValidationResult
{
    public bool IsValid { get; init; }
    public LicenseValidationFailureReason? FailureReason { get; init; }
    public string? ErrorMessage { get; init; }
    public LicenseInfo? License { get; init; }

    public static LicenseValidationResult Success(LicenseInfo license) => new()
    {
        IsValid = true,
        License = license
    };

    public static LicenseValidationResult Failure(LicenseValidationFailureReason reason, string errorMessage) => new()
    {
        IsValid = false,
        FailureReason = reason,
        ErrorMessage = errorMessage
    };
}

public enum LicenseValidationFailureReason
{
    None = 0,
    Missing = 1,
    Malformed = 2,
    InvalidSignature = 3,
    Expired = 4,
    FingerprintMismatch = 5,
    Revoked = 6,
    Invalid = 7
}
