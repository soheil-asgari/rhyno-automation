namespace OfficeAutomation.Services.Licensing;

/// <summary>
/// Singleton service providing the in-memory cached license state across the application lifetime.
/// </summary>
public interface ILicenseContext
{
    /// <summary>
    /// Current validated license information, or null if no valid license is loaded.
    /// </summary>
    LicenseInfo? CurrentLicense { get; }

    /// <summary>
    /// Latest validation result including failure reason if invalid.
    /// </summary>
    LicenseValidationResult ValidationResult { get; }

    /// <summary>
    /// True if the system has an active, valid license; false if operating in Locked mode.
    /// </summary>
    bool IsLicensed { get; }

    /// <summary>
    /// True if the application is locked due to missing, expired, mismatched or invalid license.
    /// </summary>
    bool IsLocked { get; }

    /// <summary>
    /// Human-readable lock message/banner explaining the current license status.
    /// </summary>
    string? LockReason { get; }

    /// <summary>
    /// Checks whether a given module is permitted under the active license.
    /// Always returns false if the system is locked.
    /// </summary>
    bool IsModuleEnabled(string moduleName);

    /// <summary>
    /// Loads the license file from disk (or the specified path) and re-evaluates the license context.
    /// </summary>
    Task<LicenseValidationResult> RefreshAsync(string? licenseFilePath = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Evaluates raw license content on demand (e.g. after user uploads a new license file),
    /// saves it to the standard license storage location if valid, and refreshes the in-memory context.
    /// </summary>
    Task<LicenseValidationResult> ActivateLicenseAsync(string licenseFileContent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Timestamp (UTC) of the last successful heartbeat call to the license server, if any.
    /// </summary>
    DateTime? LastSuccessfulHeartbeatAt { get; }

    /// <summary>
    /// Number of days remaining in the heartbeat grace period before lock.
    /// Null if online check is not required or license is not loaded.
    /// </summary>
    int? GraceDaysRemaining { get; }

    /// <summary>
    /// Current grace period status (NotApplicable, Normal, WarningAmber, WarningRed, Expired).
    /// </summary>
    GracePeriodStatus GraceStatus { get; }

    /// <summary>
    /// Warning message to display in UI if grace period is approaching expiration.
    /// </summary>
    string? GraceWarningBanner { get; }

    /// <summary>
    /// Updates heartbeat state (e.g. after successful or failed heartbeat attempt).
    /// </summary>
    Task UpdateHeartbeatStateAsync(Action<HeartbeatState> updateAction, CancellationToken cancellationToken = default);

    /// <summary>
    /// Immediately marks the license as Revoked and locks the system.
    /// </summary>
    void MarkRevoked(string reason);
}

