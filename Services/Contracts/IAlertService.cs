using OfficeAutomation.Models;

namespace OfficeAutomation.Services.Contracts;

public interface IAlertService
{
    /// <summary>
    /// Returns all visible system + user rules for the given module.
    /// </summary>
    Task<List<AlertRule>> GetRulesAsync(string? sourceModule = null, CancellationToken ct = default);

    /// <summary>
    /// Returns the user's subscription for each rule (with defaults for missing subscriptions).
    /// </summary>
    Task<AlertSettingsVM> GetUserAlertSettingsAsync(string userId, string? sourceModule = null, CancellationToken ct = default);

    /// <summary>
    /// Updates a user's subscription preference for a specific rule.
    /// </summary>
    Task UpdateSubscriptionAsync(string userId, int ruleId, bool receiveInApp, bool receiveEmail, CancellationToken ct = default);

    /// <summary>
    /// Ensures system-defined rules exist in the database (seeds defaults).
    /// </summary>
    Task SeedSystemRulesAsync(CancellationToken ct = default);

    /// <summary>
    /// Checks whether a user has opted in for a given trigger type's in-app notifications.
    /// Returns true by default if no subscription exists.
    /// </summary>
    Task<bool> IsSubscribedInAppAsync(string userId, string triggerType, CancellationToken ct = default);

    /// <summary>
    /// Checks whether a user has opted in for email for a given trigger type.
    /// </summary>
    Task<bool> IsSubscribedEmailAsync(string userId, string triggerType, CancellationToken ct = default);
}