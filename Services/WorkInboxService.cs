using System.Globalization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Models;
using OfficeAutomation.Modules.Identity.Infrastructure.Persistence;
using OfficeAutomation.Modules.Workflow.Infrastructure.Persistence;

namespace OfficeAutomation.Services;

public sealed class WorkInboxService
{
    private readonly IWorkflowDbContext _context;
    private readonly IdentityDbContext? _identityContext;

    public WorkInboxService(IWorkflowDbContext context, IdentityDbContext? identityContext = null)
    {
        _context = context;
        _identityContext = identityContext;
    }

    public async Task<WorkInboxVM> BuildAsync(
        string userId,
        bool canApproveGlobal,
        string? filter,
        CancellationToken cancellationToken = default)
    {
        var normalizedFilter = NormalizeFilter(filter);
        var items = new List<WorkInboxItemVM>();

        items.AddRange(await GetWorkflowItemsAsync(userId, cancellationToken));
        items.AddRange(await GetUnreadLettersAsync(userId, cancellationToken));
        items.AddRange(await GetUnreadNotificationsAsync(userId, cancellationToken));

        if (canApproveGlobal)
        {
            items.AddRange(await GetGlobalModuleItemsAsync(cancellationToken));
        }

        var filtered = normalizedFilter switch
        {
            "Approvals" => items.Where(item => item.Type == "Approval"),
            "Letters" => items.Where(item => item.Module == "Letters"),
            "Alerts" => items.Where(item => item.Type == "Alert"),
            "Overdue" => items.Where(item => item.IsOverdue),
            "DueSoon" => items.Where(item => item.SlaState == WorkflowSlaState.DueSoon),
            "Unread" => items.Where(item => !item.IsRead),
            _ => items
        };

        var ordered = filtered
            .OrderByDescending(item => item.IsOverdue)
            .ThenByDescending(item => item.RequiresAction)
            .ThenBy(item => PriorityRank(item.Priority))
            .ThenBy(item => item.Deadline ?? DateTimeOffset.MaxValue)
            .ThenByDescending(item => item.SortDate)
            .Take(200)
            .ToList();

        return new WorkInboxVM
        {
            ActiveFilter = normalizedFilter,
            CurrentUserId = userId,
            TotalCount = items.Count,
            ApprovalCount = items.Count(item => item.Type == "Approval"),
            LetterCount = items.Count(item => item.Module == "Letters"),
            AlertCount = items.Count(item => item.Type == "Alert"),
            OverdueCount = items.Count(item => item.IsOverdue),
            DueSoonCount = items.Count(item => item.SlaState == WorkflowSlaState.DueSoon),
            UnreadCount = items.Count(item => !item.IsRead),
            Assignees = await GetAssigneesAsync(cancellationToken),
            Items = ordered
        };
    }

    private async Task<List<WorkInboxAssigneeVM>> GetAssigneesAsync(CancellationToken cancellationToken)
    {
        return await _context.Users
            .AsNoTracking()
            .OrderBy(item => item.FullName)
            .ThenBy(item => item.UserName)
            .Select(item => new WorkInboxAssigneeVM
            {
                UserId = item.Id,
                DisplayName = string.IsNullOrWhiteSpace(item.FullName) ? item.UserName ?? item.Email ?? item.Id : item.FullName
            })
            .Take(200)
            .ToListAsync(cancellationToken);
    }

    private async Task<List<WorkInboxItemVM>> GetWorkflowItemsAsync(string userId, CancellationToken cancellationToken)
    {
        var userRoleIds = await GetUserRoleIdsAsync(userId, cancellationToken);

        var departmentId = await _context.Users
            .AsNoTracking()
            .Where(item => item.Id == userId)
            .Select(item => item.DepartmentId)
            .FirstOrDefaultAsync(cancellationToken);

        var rows = await _context.WorkflowSteps
            .AsNoTracking()
            .Include(item => item.AssignedToUser)
            .Include(item => item.WorkflowInstance)
            .Where(item =>
                item.CompletedAt == null &&
                item.WorkflowInstance != null &&
                (item.AssignedToUserId == userId ||
                 (item.AssignedRoleId != null && userRoleIds.Contains(item.AssignedRoleId)) ||
                 (departmentId != null && item.AssignedDepartmentId == departmentId)))
            .OrderBy(item => item.DueAt ?? DateTimeOffset.MaxValue)
            .ThenByDescending(item => item.CreatedAt)
            .ToListAsync(cancellationToken);

        return rows.Select(item =>
        {
            var documentType = item.WorkflowInstance!.DocumentType;
            var documentId = item.WorkflowInstance.DocumentId;
            var title = $"{documentType} #{documentId}";
            var description = string.IsNullOrWhiteSpace(item.StepName) ? $"اقدام مرحله {item.StepNumber} گردش کار" : item.StepName!;
            var slaState = NormalizeSla(item.SlaState, item.DueAt);
            var isOverdue = slaState is WorkflowSlaState.Overdue or WorkflowSlaState.Breached;

            return new WorkInboxItemVM
            {
                Id = $"workflow-step-{item.Id}",
                Type = "Approval",
                Module = documentType,
                Title = title,
                Description = description,
                Status = item.Status,
                StatusCssClass = WorkflowStatus.BadgeCss(item.Status),
                Priority = isOverdue || slaState == WorkflowSlaState.DueSoon ? "High" : "Normal",
                PriorityCssClass = isOverdue || slaState == WorkflowSlaState.DueSoon ? "inbox-priority-high" : "inbox-priority-normal",
                GroupLabel = documentType,
                CreatedAtText = ToPersianDate(item.CreatedAt),
                SortDate = item.CreatedAt,
                Url = ResolveWorkflowDocumentUrl(documentType, documentId),
                Icon = "bi-diagram-3",
                RequiresAction = true,
                IsRead = item.ReadAt != null,
                SenderName = item.WorkflowInstance.StartedByUserId,
                SlaState = slaState,
                SlaText = BuildSlaText(item.DueAt, slaState),
                Deadline = item.DueAt,
                IsOverdue = isOverdue,
                IsExpired = item.DueAt.HasValue && item.DueAt.Value < DateTimeOffset.UtcNow,
                SearchText = string.Join(" ", title, description, documentType, item.Status, item.AssignedToUser?.FullName ?? string.Empty),
                DetailTitle = title,
                DetailSummary = description,
                DetailCaption = item.AssignedToUser?.FullName,
                DocumentType = documentType,
                DocumentId = documentId,
                StepNumber = item.StepNumber,
                WorkflowStepId = item.Id,
                CanInlineApprove = true,
                CanInlineReject = true,
                CanInlineComment = true,
                CanInlineDelegate = true
            };
        }).ToList();
    }

    private async Task<List<string>> GetUserRoleIdsAsync(string userId, CancellationToken cancellationToken)
    {
        var userRoles = _identityContext?.UserRoles.AsNoTracking()
            ?? (_context as DbContext)?.Set<IdentityUserRole<string>>().AsNoTracking();
        if (userRoles == null)
        {
            return [];
        }

        return await userRoles
            .Where(item => item.UserId == userId)
            .Select(item => item.RoleId)
            .ToListAsync(cancellationToken);
    }

    private async Task<List<WorkInboxItemVM>> GetUnreadLettersAsync(string userId, CancellationToken cancellationToken)
    {
        var letters = await _context.Letters
            .AsNoTracking()
            .Include(item => item.Sender)
            .Where(item => !item.IsRead && (item.ReceiverId == userId || item.FinalReceiverId == userId))
            .OrderByDescending(item => item.SentDate)
            .Take(40)
            .ToListAsync(cancellationToken);

        return letters.Select(item => new WorkInboxItemVM
            {
                Id = "letter-unread-" + item.Id,
                Type = "Message",
                Module = "Letters",
                Title = item.Title,
                Description = item.Sender != null ? "فرستنده: " + item.Sender.FullName : "نامه خوانده‌نشده",
                Status = item.WorkflowStatus,
                StatusCssClass = WorkflowStatus.BadgeCss(item.WorkflowStatus),
                Priority = "Normal",
                PriorityCssClass = "inbox-priority-normal",
                GroupLabel = "نامه‌های خوانده‌نشده",
                CreatedAtText = ToPersianDate(new DateTimeOffset(item.SentDate)),
                SortDate = new DateTimeOffset(item.SentDate),
                Url = "/Letters/Details/" + item.Id,
                Icon = "bi-envelope-paper",
                RequiresAction = true,
                IsRead = false,
                SenderName = item.Sender != null ? item.Sender.FullName : null,
                SlaState = WorkflowSlaState.OnTrack,
                SlaText = "بدون SLA",
                SearchText = string.Join(" ", item.Title, item.Sender != null ? item.Sender.FullName : string.Empty, item.WorkflowStatus, "Letters"),
                DetailTitle = item.Title,
                DetailSummary = item.Sender != null ? "فرستنده: " + item.Sender.FullName : "نامه خوانده‌نشده",
                DetailCaption = "برای جزئیات کامل، نامه را باز کنید."
            })
            .ToList();
    }

    private async Task<List<WorkInboxItemVM>> GetUnreadNotificationsAsync(string userId, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var notifications = await _context.Notifications
            .AsNoTracking()
            .Where(item => item.RecipientUserId == userId && !item.IsRead && (item.ExpiresAt == null || item.ExpiresAt > now))
            .OrderByDescending(item => item.CreatedAt)
            .Take(40)
            .ToListAsync(cancellationToken);

        return notifications.Select(item => new WorkInboxItemVM
            {
                Id = "notification-" + item.Id,
                Type = "Alert",
                Module = item.SourceModule ?? "Notifications",
                Title = item.Title,
                Description = item.Message,
                Status = item.Severity,
                StatusCssClass = "text-bg-info",
                Priority = item.Severity == NotificationSeverity.Danger || item.Severity == NotificationSeverity.Warning ? "High" : "Normal",
                PriorityCssClass = item.Severity == NotificationSeverity.Danger || item.Severity == NotificationSeverity.Warning ? "inbox-priority-high" : "inbox-priority-normal",
                GroupLabel = "اعلان‌ها",
                CreatedAtText = ToPersianDate(item.CreatedAt),
                SortDate = item.CreatedAt,
                Url = item.LinkUrl ?? "/Notifications",
                Icon = NotificationSeverity.Icon(item.Severity),
                RequiresAction = item.Severity == NotificationSeverity.Danger || item.Severity == NotificationSeverity.Warning,
                IsRead = false,
                SlaState = WorkflowSlaState.OnTrack,
                SlaText = item.ExpiresAt.HasValue ? $"انقضا: {ToPersianDate(item.ExpiresAt.Value)}" : "بدون انقضا",
                Deadline = item.ExpiresAt,
                IsExpired = item.ExpiresAt.HasValue && item.ExpiresAt.Value <= now,
                IsOverdue = false,
                SearchText = string.Join(" ", item.Title, item.Message, item.SourceModule ?? "Notifications", item.Severity),
                DetailTitle = item.Title,
                DetailSummary = item.Message,
                DetailCaption = item.LinkUrl
            })
            .ToList();
    }

    private async Task<List<WorkInboxItemVM>> GetGlobalModuleItemsAsync(CancellationToken cancellationToken)
    {
        var items = new List<WorkInboxItemVM>();

        var leaves = await _context.Leaves
            .AsNoTracking()
            .Include(item => item.User)
            .Where(item => item.Status == WorkflowStatus.PendingApproval)
            .OrderBy(item => item.StartDate)
            .Take(20)
            .ToListAsync(cancellationToken);

        items.AddRange(leaves.Select(item => new WorkInboxItemVM
            {
                Id = "leave-approval-" + item.Id,
                Type = "Approval",
                Module = "Leaves",
                Title = item.User != null ? "مرخصی " + item.User.FullName : "درخواست مرخصی",
                Description = item.Reason,
                Status = item.Status,
                StatusCssClass = WorkflowStatus.BadgeCss(item.Status),
                Priority = "High",
                PriorityCssClass = "inbox-priority-high",
                GroupLabel = "مرخصی‌ها",
                CreatedAtText = ToPersianDate(new DateTimeOffset(item.StartDate)),
                SortDate = new DateTimeOffset(item.StartDate),
                Url = "/Leaves",
                Icon = "bi-calendar-check",
                RequiresAction = true,
                IsRead = true,
                SlaState = WorkflowSlaState.OnTrack,
                SlaText = "بدون SLA",
                SearchText = string.Join(" ", item.User != null ? item.User.FullName : string.Empty, item.Reason, "Leave", item.Status),
                DetailTitle = item.User != null ? "مرخصی " + item.User.FullName : "درخواست مرخصی",
                DetailSummary = item.Reason,
                DetailCaption = "تایید امنیت/مدیریت",
                DocumentType = nameof(Leave),
                DocumentId = item.Id,
                StepNumber = 1,
                CanInlineApprove = true,
                CanInlineReject = true
            })
            .ToList());

        return items;
    }

    private static string NormalizeFilter(string? filter)
    {
        return filter?.Trim() switch
        {
            "Approvals" => "Approvals",
            "Letters" => "Letters",
            "Alerts" => "Alerts",
            "Overdue" => "Overdue",
            "DueSoon" => "DueSoon",
            "Unread" => "Unread",
            _ => "All"
        };
    }

    private static int PriorityRank(string priority) => priority == "High" ? 0 : 1;

    private static string ToPersianDate(DateTimeOffset value)
    {
        var local = value.ToLocalTime().DateTime;
        var calendar = new PersianCalendar();
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{calendar.GetYear(local):0000}/{calendar.GetMonth(local):00}/{calendar.GetDayOfMonth(local):00}");
    }

    private static string ResolveWorkflowDocumentUrl(string documentType, int documentId)
    {
        return documentType switch
        {
            "Letter" => "/Letters/Details/" + documentId,
            "Invoice" => "/Financial/EditInvoice/" + documentId,
            "InventoryTransferRequest" => "/Warehouse/TransferRequestDetails/" + documentId,
            "Leave" => "/Leaves/Details/" + documentId,
            _ => "/WorkInbox"
        };
    }

    private static string NormalizeSla(string? state, DateTimeOffset? dueAt)
    {
        if (!string.IsNullOrWhiteSpace(state))
        {
            return state;
        }

        if (!dueAt.HasValue)
        {
            return WorkflowSlaState.OnTrack;
        }

        if (dueAt.Value <= DateTimeOffset.UtcNow.AddHours(-1))
        {
            return WorkflowSlaState.Breached;
        }

        if (dueAt.Value < DateTimeOffset.UtcNow)
        {
            return WorkflowSlaState.Overdue;
        }

        if (dueAt.Value <= DateTimeOffset.UtcNow.AddHours(6))
        {
            return WorkflowSlaState.DueSoon;
        }

        return WorkflowSlaState.OnTrack;
    }

    private static string BuildSlaText(DateTimeOffset? dueAt, string slaState)
    {
        if (!dueAt.HasValue)
        {
            return "بدون SLA";
        }

        return slaState switch
        {
            WorkflowSlaState.Breached => "نقض SLA",
            WorkflowSlaState.Overdue => "تاخیر در اقدام",
            WorkflowSlaState.DueSoon => "نزدیک به موعد",
            _ => $"مهلت: {ToPersianDate(dueAt.Value)}"
        };
    }
}
