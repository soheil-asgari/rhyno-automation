using Microsoft.AspNetCore.Mvc.Rendering;

namespace OfficeAutomation.Models;

/// <summary>
/// ViewModel for the Notification Center page.
/// </summary>
public sealed class NotificationCenterIndexVM
{
    public string? SeverityFilter { get; set; }
    public bool? ShowUnreadOnly { get; set; }
    public string? SourceModuleFilter { get; set; }

    public int UnreadCount { get; set; }
    public int TotalCount { get; set; }

    public IReadOnlyList<NotificationListItemVM> Items { get; set; } = [];

    public List<SelectListItem> SeverityOptions { get; set; } = [];
    public List<SelectListItem> ModuleOptions { get; set; } = [];
}

/// <summary>
/// ViewModel for the Alert Rule Settings page.
/// </summary>
public sealed class AlertSettingsVM
{
    public IReadOnlyList<AlertRuleVM> Rules { get; set; } = [];
}

public sealed class AlertRuleVM
{
    public int Id { get; set; }
    public string TriggerType { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Severity { get; set; } = NotificationSeverity.Info;
    public bool IsActive { get; set; }
    public bool IsSystemRule { get; set; }
    public bool SendInApp { get; set; }
    public bool SendEmail { get; set; }
    public int? AdvanceNoticeDays { get; set; }

    // User subscription state (defaults to rule defaults)
    public bool SubscribedInApp { get; set; } = true;
    public bool SubscribedEmail { get; set; }
}

public sealed class AlertSubscriptionUpdateVM
{
    public int RuleId { get; set; }
    public bool ReceiveInApp { get; set; }
    public bool ReceiveEmail { get; set; }
}