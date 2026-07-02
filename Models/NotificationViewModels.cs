namespace OfficeAutomation.Models;

public sealed class NotificationListItemVM
{
    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string Severity { get; init; } = NotificationSeverity.Info;
    public string Icon { get; init; } = "bi-info-circle";
    public string CssTone { get; init; } = "info";
    public string? LinkUrl { get; init; }
    public string SourceModule { get; init; } = string.Empty;
    public bool IsRead { get; init; }
    public string CreatedAtText { get; init; } = string.Empty;
}

public sealed class NotificationCenterVM
{
    public int TotalCount { get; init; }
    public int UnreadCount { get; init; }
    public IReadOnlyList<NotificationListItemVM> Items { get; init; } = [];
}

public sealed class HeaderNotificationVM
{
    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string Url { get; init; } = "#";
    public string Icon { get; init; } = "bi-info-circle";
    public string Tone { get; init; } = "info";
}
