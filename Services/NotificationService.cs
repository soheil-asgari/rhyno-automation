using System.Globalization;
using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Modules.Office.Infrastructure.Persistence;
using OfficeAutomation.Models;

namespace OfficeAutomation.Services;

public sealed class NotificationService
{
    private readonly OfficeDbContext _context;
    private readonly Tenancy.ICurrentTenantAccessor? _currentTenantAccessor;

    public NotificationService(OfficeDbContext context, Tenancy.ICurrentTenantAccessor? currentTenantAccessor = null)
    {
        _context = context;
        _currentTenantAccessor = currentTenantAccessor;
    }

    public async Task<Notification> CreateAsync(
        string recipientUserId,
        string title,
        string message,
        string? severity = null,
        string? linkUrl = null,
        string? sourceModule = null,
        string? sourceEntityType = null,
        int? sourceEntityId = null,
        DateTimeOffset? expiresAt = null,
        CancellationToken cancellationToken = default)
    {
        var notification = new Notification
        {
            TenantId = _currentTenantAccessor?.TenantId,
            RecipientUserId = recipientUserId,
            Title = title.Trim(),
            Message = message.Trim(),
            Severity = NotificationSeverity.Normalize(severity),
            LinkUrl = string.IsNullOrWhiteSpace(linkUrl) ? null : linkUrl.Trim(),
            SourceModule = string.IsNullOrWhiteSpace(sourceModule) ? null : sourceModule.Trim(),
            SourceEntityType = string.IsNullOrWhiteSpace(sourceEntityType) ? null : sourceEntityType.Trim(),
            SourceEntityId = sourceEntityId,
            ExpiresAt = expiresAt
        };

        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync(cancellationToken);
        return notification;
    }

    public async Task<Notification> UpsertActiveAsync(
        string recipientUserId,
        string title,
        string message,
        string? severity = null,
        string? linkUrl = null,
        string? sourceModule = null,
        string? sourceEntityType = null,
        int? sourceEntityId = null,
        DateTimeOffset? expiresAt = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedSeverity = NotificationSeverity.Normalize(severity);
        var normalizedSourceModule = string.IsNullOrWhiteSpace(sourceModule) ? null : sourceModule.Trim();
        var normalizedSourceEntityType = string.IsNullOrWhiteSpace(sourceEntityType) ? null : sourceEntityType.Trim();
        var now = DateTimeOffset.UtcNow;

        var notification = await _context.Notifications
            .FirstOrDefaultAsync(item =>
                item.RecipientUserId == recipientUserId &&
                item.SourceModule == normalizedSourceModule &&
                item.SourceEntityType == normalizedSourceEntityType &&
                item.SourceEntityId == sourceEntityId &&
                (item.ExpiresAt == null || item.ExpiresAt > now),
                cancellationToken);

        if (notification == null)
        {
            return await CreateAsync(
                recipientUserId,
                title,
                message,
                normalizedSeverity,
                linkUrl,
                normalizedSourceModule,
                normalizedSourceEntityType,
                sourceEntityId,
                expiresAt,
                cancellationToken);
        }

        notification.Title = title.Trim();
        notification.Message = message.Trim();
        notification.Severity = normalizedSeverity;
        notification.LinkUrl = string.IsNullOrWhiteSpace(linkUrl) ? null : linkUrl.Trim();
        notification.ExpiresAt = expiresAt;
        await _context.SaveChangesAsync(cancellationToken);
        return notification;
    }

    public async Task<IReadOnlyList<HeaderNotificationVM>> GetHeaderNotificationsAsync(
        string userId,
        int take = 8,
        CancellationToken cancellationToken = default)
    {
        return await ActiveNotifications(userId)
            .Where(item => !item.IsRead)
            .OrderByDescending(item => item.CreatedAt)
            .Take(take)
            .Select(item => new HeaderNotificationVM
            {
                Id = item.Id,
                Title = item.Title,
                Message = item.Message,
                Url = item.LinkUrl ?? "/Notifications",
                Icon = NotificationSeverity.Icon(item.Severity),
                Tone = NotificationSeverity.CssTone(item.Severity)
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<NotificationCenterVM> BuildCenterAsync(
        string userId,
        int take = 80,
        CancellationToken cancellationToken = default)
    {
        var query = ActiveNotifications(userId);
        var totalCount = await query.CountAsync(cancellationToken);
        var unreadCount = await query.CountAsync(item => !item.IsRead, cancellationToken);

        var items = await query
            .OrderBy(item => item.IsRead)
            .ThenByDescending(item => item.CreatedAt)
            .Take(take)
            .Select(item => new NotificationListItemVM
            {
                Id = item.Id,
                Title = item.Title,
                Message = item.Message,
                Severity = item.Severity,
                Icon = NotificationSeverity.Icon(item.Severity),
                CssTone = NotificationSeverity.CssTone(item.Severity),
                LinkUrl = item.LinkUrl,
                SourceModule = item.SourceModule ?? string.Empty,
                IsRead = item.IsRead,
                CreatedAtText = ToPersianDate(item.CreatedAt)
            })
            .ToListAsync(cancellationToken);

        return new NotificationCenterVM
        {
            TotalCount = totalCount,
            UnreadCount = unreadCount,
            Items = items
        };
    }

    public async Task MarkReadAsync(int notificationId, string userId, CancellationToken cancellationToken = default)
    {
        var notification = await _context.Notifications
            .FirstOrDefaultAsync(item => item.Id == notificationId && item.RecipientUserId == userId, cancellationToken);

        if (notification == null || notification.IsRead)
        {
            return;
        }

        notification.IsRead = true;
        notification.ReadAt = DateTimeOffset.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkAllReadAsync(string userId, CancellationToken cancellationToken = default)
    {
        var notifications = await ActiveNotifications(userId)
            .Where(item => !item.IsRead)
            .ToListAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        foreach (var notification in notifications)
        {
            notification.IsRead = true;
            notification.ReadAt = now;
        }

        if (notifications.Count > 0)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    private IQueryable<Notification> ActiveNotifications(string userId)
    {
        var now = DateTimeOffset.UtcNow;
        return _context.Notifications
            .AsQueryable()
            .Where(item => item.RecipientUserId == userId && (item.ExpiresAt == null || item.ExpiresAt > now));
    }

    private static string ToPersianDate(DateTimeOffset value)
    {
        var local = value.ToLocalTime().DateTime;
        var calendar = new PersianCalendar();
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{calendar.GetYear(local):0000}/{calendar.GetMonth(local):00}/{calendar.GetDayOfMonth(local):00}");
    }
}
