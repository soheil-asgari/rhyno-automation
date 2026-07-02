using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Modules.Identity.Infrastructure.Persistence;
using OfficeAutomation.Modules.Platform.Infrastructure.Persistence;
using OfficeAutomation.Models;

namespace OfficeAutomation.Services;

public sealed class SecurityAuditNotificationService
{
    private readonly PlatformDbContext _context;
    private readonly IdentityDbContext _identityContext;
    private readonly NotificationService _notificationService;

    public SecurityAuditNotificationService(
        PlatformDbContext context,
        IdentityDbContext identityContext,
        NotificationService notificationService)
    {
        _context = context;
        _identityContext = identityContext;
        _notificationService = notificationService;
    }

    public async Task PublishRecentSensitiveEventsAsync(CancellationToken cancellationToken = default)
    {
        var since = DateTimeOffset.UtcNow.AddHours(-24);
        var events = await _context.AuditLogs
            .AsNoTracking()
            .Where(item => item.DateTime >= since)
            .Where(item => item.IsSensitive || item.Action == "Delete")
            .OrderByDescending(item => item.DateTime)
            .Take(20)
            .Select(item => new
            {
                item.Id,
                item.Action,
                item.TableName,
                item.UserId,
                item.UserIP,
                item.AffectedColumns,
                item.DateTime,
                item.IsSensitive
            })
            .ToListAsync(cancellationToken);

        if (events.Count == 0)
        {
            return;
        }

        var recipientIds = await GetSecurityNotificationRecipientsAsync(cancellationToken);
        if (recipientIds.Count == 0)
        {
            return;
        }

        var expiresAt = DateTimeOffset.UtcNow.AddDays(14);
        foreach (var auditEvent in events)
        {
            var title = auditEvent.Action == "Delete"
                ? "رخداد حذف حساس ثبت شد"
                : "رخداد امنیتی حساس ثبت شد";
            var message = BuildMessage(
                auditEvent.Action,
                auditEvent.TableName,
                auditEvent.UserId,
                auditEvent.UserIP,
                auditEvent.AffectedColumns);

            foreach (var recipientId in recipientIds)
            {
                await _notificationService.UpsertActiveAsync(
                    recipientId,
                    title,
                    message,
                    NotificationSeverity.Danger,
                    "/admin/audit-dashboard",
                    "Security",
                    "AuditLog",
                    StableSourceId(auditEvent.Id),
                    expiresAt,
                    cancellationToken);
            }
        }
    }

    private async Task<List<string>> GetSecurityNotificationRecipientsAsync(CancellationToken cancellationToken)
    {
        var roleBasedUsers =
            from userRole in _identityContext.UserRoles.AsNoTracking()
            join rolePermission in _identityContext.RolePermissions.AsNoTracking()
                on userRole.RoleId equals rolePermission.RoleId
            where rolePermission.PermissionKey == "Security.Manage" || rolePermission.PermissionKey == "AuditLogs.Read"
            select userRole.UserId;

        var flagBasedUsers = _identityContext.Users
            .AsNoTracking()
            .Where(user => user.CanAccessSystemSettings)
            .Select(user => user.Id);

        return await roleBasedUsers
            .Union(flagBasedUsers)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    private static string BuildMessage(
        string action,
        string tableName,
        string? userId,
        string? userIp,
        string? affectedColumns)
    {
        var actor = string.IsNullOrWhiteSpace(userId) ? "سیستم" : userId;
        var ip = string.IsNullOrWhiteSpace(userIp) ? "IP نامشخص" : userIp;
        var columns = string.IsNullOrWhiteSpace(affectedColumns) ? "ستون نامشخص" : affectedColumns;
        return $"{action} روی {tableName} توسط {actor} از {ip} | {columns}";
    }

    private static int StableSourceId(Guid value)
    {
        var bytes = value.ToByteArray();
        return BitConverter.ToInt32(bytes, 0);
    }
}

