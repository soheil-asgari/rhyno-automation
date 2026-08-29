using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OfficeAutomation.Models;
using OfficeAutomation.Services;
using OfficeAutomation.Services.Contracts;
using OfficeAutomation.Services.Security;
using System.Security.Claims;

namespace OfficeAutomation.Controllers;

[Authorize]
[Route("[controller]")]
public class NotificationsController : Controller
{
    private readonly NotificationService _notificationService;
    private readonly IAlertService _alertService;
    private readonly ILogger<NotificationsController> _logger;

    public NotificationsController(
        NotificationService notificationService,
        IAlertService alertService,
        ILogger<NotificationsController> logger)
    {
        _notificationService = notificationService;
        _alertService = alertService;
        _logger = logger;
    }

    private string GetCurrentUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException("کاربر احراز هویت نشده است.");
    }

    // ══════════════════════════════════════════════════════════
    //  Notification Center
    // ══════════════════════════════════════════════════════════

    [HttpGet("")]
    public async Task<IActionResult> Index(
        string? severity = null,
        string? module = null,
        bool? unreadOnly = null,
        CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        var center = await _notificationService.BuildCenterAsync(
            userId,
            take: 100,
            filterBySeverity: severity,
            filterByModule: module,
            showUnreadOnly: unreadOnly ?? (severity == null && module == null ? null : false),
            ct);

        var vm = new NotificationCenterIndexVM
        {
            Items = center.Items,
            UnreadCount = center.UnreadCount,
            TotalCount = center.TotalCount,
            SeverityFilter = severity ?? "",
            SourceModuleFilter = module ?? "",
            ShowUnreadOnly = unreadOnly,
            SeverityOptions = new()
            {
                new("", "همه"),
                new(NotificationSeverity.Info, NotificationSeverity.Info),
                new(NotificationSeverity.Success, NotificationSeverity.Success),
                new(NotificationSeverity.Warning, NotificationSeverity.Warning),
                new(NotificationSeverity.Danger, NotificationSeverity.Danger),
            },
            ModuleOptions = new()
            {
                new("", "همه ماژول‌ها"),
                new("Contracts", "قراردادها"),
                new("Approvals", "تأییدها"),
                new("PaymentSchedule", "برنامه پرداخت"),
                new("System", "سیستم"),
            }
        };

        return View(vm);
    }

    // ══════════════════════════════════════════════════════════
    //  AJAX: Unread Count (for header badge polling)
    // ══════════════════════════════════════════════════════════

    [HttpGet("UnreadCount")]
    public async Task<IActionResult> UnreadCount(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var center = await _notificationService.BuildCenterAsync(userId, take: 1, cancellationToken: ct);
        return Json(new { count = center.UnreadCount });
    }

    // ══════════════════════════════════════════════════════════
    //  AJAX: Mark Single Notification as Read
    // ══════════════════════════════════════════════════════════

    [HttpPost("MarkRead/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkRead(int id, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        await _notificationService.MarkReadAsync(id, userId, ct);
        return Json(new { success = true });
    }

    // ══════════════════════════════════════════════════════════
    //  AJAX: Mark All Notifications as Read
    // ══════════════════════════════════════════════════════════

    [HttpPost("MarkAllRead")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        await _notificationService.MarkAllReadAsync(userId, ct);
        return Json(new { success = true });
    }

    // ══════════════════════════════════════════════════════════
    //  AJAX: Get Notifications as JSON (for header panel)
    // ══════════════════════════════════════════════════════════

    [HttpGet("GetNotificationsJson")]
    public async Task<IActionResult> GetNotificationsJson(
        string? module = null,
        string? severity = null,
        bool? unreadOnly = null,
        int take = 5,
        CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        var center = await _notificationService.BuildCenterAsync(
            userId,
            take: take,
            filterBySeverity: severity,
            filterByModule: module,
            showUnreadOnly: unreadOnly ?? true,
            ct);
        return Json(new { items = center.Items, totalCount = center.TotalCount, unreadCount = center.UnreadCount });
    }

    // ══════════════════════════════════════════════════════════
    //  Alert Rules Settings Page
    // ══════════════════════════════════════════════════════════

    [HttpGet("AlertSettings")]
    [PermissionAuthorize("Notification.AlertSettings")]
    public async Task<IActionResult> AlertSettings(string? sourceModule, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var vm = await _alertService.GetUserAlertSettingsAsync(userId, sourceModule ?? "Contracts", ct);
        return View(vm);
    }

    // ══════════════════════════════════════════════════════════
    //  AJAX: Update Alert Subscription
    // ══════════════════════════════════════════════════════════

    [HttpPost("UpdateSubscription")]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize("Notification.AlertSettings")]
    public async Task<IActionResult> UpdateSubscription([FromBody] AlertSubscriptionUpdateVM model, CancellationToken ct)
    {
        if (!ModelState.IsValid) return Json(new { success = false });
        var userId = GetCurrentUserId();
        await _alertService.UpdateSubscriptionAsync(userId, model.RuleId, model.ReceiveInApp, model.ReceiveEmail, ct);
        return Json(new { success = true });
    }
}


