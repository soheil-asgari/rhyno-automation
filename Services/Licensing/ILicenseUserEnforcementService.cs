namespace OfficeAutomation.Services.Licensing;

/// <summary>
/// Status and user count usage for a licensed module or the global system.
/// </summary>
public sealed class ModuleUserUsage
{
    public string ModuleName { get; init; } = string.Empty;
    public string ModuleDisplayName { get; init; } = string.Empty;
    public int ActiveUserCount { get; init; }
    public int MaxUsersAllowed { get; init; }
    public bool IsExceeded => ActiveUserCount > MaxUsersAllowed;
    public bool IsAtLimit => ActiveUserCount >= MaxUsersAllowed;
}

/// <summary>
/// Result of evaluating whether a user can be added or assigned to a module/role.
/// </summary>
public sealed class LicenseUserValidationResult
{
    public bool Allowed { get; init; }
    public string? Reason { get; init; }
    public string? ModuleName { get; init; }
    public int CurrentCount { get; init; }
    public int MaxAllowed { get; init; }

    public static LicenseUserValidationResult Success() => new() { Allowed = true };

    public static LicenseUserValidationResult LimitReached(string moduleName, int current, int max, string? customMessage = null)
    {
        var message = customMessage ?? $"شما به سقف مجاز کاربران این ماژول رسیده‌اید: {current} از {max}";
        return new()
        {
            Allowed = false,
            Reason = message,
            ModuleName = moduleName,
            CurrentCount = current,
            MaxAllowed = max
        };
    }
}

/// <summary>
/// Service that checks and enforces named-user licensing limits per module and globally.
/// </summary>
public interface ILicenseUserEnforcementService
{
    /// <summary>
    /// Checks if a new user or role assignment can be granted without exceeding module or global limits.
    /// </summary>
    Task<LicenseUserValidationResult> ValidateUserCreationOrRoleAssignmentAsync(
        string? existingUserId,
        IEnumerable<string> targetRoleNames,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculates current active user usage vs. limit for each licensed module and globally.
    /// </summary>
    Task<IReadOnlyList<ModuleUserUsage>> GetModuleUserUsagesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if any licensed module or global user count is currently exceeded (e.g. following a license downgrade).
    /// </summary>
    Task<IReadOnlyList<ModuleUserUsage>> GetDowngradedExceededModulesAsync(CancellationToken cancellationToken = default);
}
