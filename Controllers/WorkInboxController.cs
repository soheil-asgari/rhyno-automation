using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Models;
using OfficeAutomation.Modules.Identity.Infrastructure.Persistence;
using OfficeAutomation.Modules.Workflow.Infrastructure.Persistence;
using OfficeAutomation.Services;
using OfficeAutomation.Services.Security;
using Microsoft.AspNetCore.Hosting;
using OfficeAutomation.Services.Tenancy;

namespace OfficeAutomation.Controllers;

[Authorize]
public sealed class WorkInboxController : Controller
{
    private readonly WorkInboxService _workInboxService;
    private readonly IAuthorizationFacade _authorizationFacade;
    private readonly WorkflowService _workflowService;
    private readonly IWorkflowDbContext _context;
    private readonly IdentityDbContext _identityContext;
    private readonly IWebHostEnvironment _environment;
    private readonly ITenantPathResolver _tenantPathResolver;
    private readonly IWorkflowExecutionService _workflowExecutionService;
    private readonly NotificationService _notificationService;
    private readonly IEmailService _emailService;

    public WorkInboxController(
        WorkInboxService workInboxService,
        IAuthorizationFacade authorizationFacade,
        WorkflowService workflowService,
        IWorkflowDbContext context,
        IdentityDbContext identityContext,
        IWebHostEnvironment environment,
        ITenantPathResolver tenantPathResolver,
        IWorkflowExecutionService workflowExecutionService,
        NotificationService notificationService,
        IEmailService emailService)
    {
        _workInboxService = workInboxService;
        _authorizationFacade = authorizationFacade;
        _workflowService = workflowService;
        _context = context;
        _identityContext = identityContext;
        _environment = environment;
        _tenantPathResolver = tenantPathResolver;
        _workflowExecutionService = workflowExecutionService;
        _notificationService = notificationService;
        _emailService = emailService;
    }

    public async Task<IActionResult> Index(string? filter, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Challenge();
        }

        var canApproveGlobal = await _authorizationFacade.IsSecurityAdminAsync();
        var model = await _workInboxService.BuildAsync(userId, canApproveGlobal, filter, cancellationToken);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ProcessTask(int taskId, string triggerAction, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized(new { success = false, message = "کاربر وارد نشده است." });
        }

        var task = await _context.WorkflowCaseTasks
            .FirstOrDefaultAsync(t => t.Id == taskId, cancellationToken);

        if (task == null)
        {
            return NotFound(new { success = false, message = "کار مورد نظر یافت نشد." });
        }

        // Verify task is assigned to user or their role/department
        var userRoleIds = await _identityContext.UserRoles
            .AsNoTracking()
            .Where(item => item.UserId == userId)
            .Select(item => item.RoleId)
            .ToListAsync(cancellationToken);

        var departmentId = await _context.Users
            .AsNoTracking()
            .Where(item => item.Id == userId)
            .Select(item => item.DepartmentId)
            .FirstOrDefaultAsync(cancellationToken);

        var step = task.WorkflowStepId.HasValue
            ? await _context.WorkflowSteps.FirstOrDefaultAsync(s => s.Id == task.WorkflowStepId.Value, cancellationToken)
            : null;

        var isAssigned = task.AssignedToUserId == userId ||
                         (step != null && step.AssignedRoleId != null && userRoleIds.Contains(step.AssignedRoleId)) ||
                         (step != null && departmentId != null && step.AssignedDepartmentId == departmentId);

        if (!isAssigned)
        {
            return Forbid();
        }

        try
        {
            await _workflowExecutionService.ExecuteNextStepAsync(task.WorkflowInstanceId, triggerAction, cancellationToken);

            task.Status = WorkflowCaseTaskStatus.Completed;
            task.CompletedAt = DateTimeOffset.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);

            return Json(new { success = true, message = "اقدام با موفقیت انجام شد." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Decide(
        string documentType,
        int documentId,
        int stepNumber,
        string decisionType,
        string? note,
        string? signatureText,
        string? forwardToUserId,
        List<IFormFile>? attachments,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Challenge();
        }

        var succeeded = await _workflowService.ExecuteDecisionAsync(
            documentType,
            documentId,
            stepNumber,
            userId,
            decisionType,
            note,
            signatureText,
            attachments,
            forwardToUserId,
            cancellationToken);

        TempData["WorkInboxMessage"] = succeeded ? "اقدام با موفقیت ثبت شد." : "ثبت اقدام ناموفق بود.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BulkAction(WorkInboxBulkActionVM model, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Challenge();
        }

        var selectedIds = model.SelectedIds
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (selectedIds.Count == 0)
        {
            TempData["WorkInboxError"] = "هیچ موردی انتخاب نشده است.";
            return RedirectToIndex(model.Filter);
        }

        var succeeded = 0;
        var unauthorized = new List<string>();
        var failed = new List<string>();
        var action = (model.Action ?? string.Empty).Trim();
        await using var transaction = _context is DbContext dbContext
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;

        foreach (var selectedId in selectedIds)
        {
            var result = action switch
            {
                "approve" => await ApplyWorkflowDecisionAsync(selectedId, userId, WorkflowDecisionType.Approve, model.Note, cancellationToken),
                "reject" => await ApplyWorkflowDecisionAsync(selectedId, userId, WorkflowDecisionType.Reject, model.Note, cancellationToken),
                "mark-read" => await SetReadStateAsync(selectedId, userId, true, cancellationToken),
                "mark-unread" => await SetReadStateAsync(selectedId, userId, false, cancellationToken),
                "delegate" => await DelegateWorkflowItemAsync(selectedId, userId, model.ToUserId, model.Note, cancellationToken),
                _ => BulkItemResult.Failed("اقدام گروهی نامعتبر است.")
            };

            if (result.Succeeded)
            {
                succeeded++;
            }
            else if (result.Unauthorized)
            {
                unauthorized.Add(result.Label);
            }
            else
            {
                failed.Add(result.Label);
            }
        }

        if (unauthorized.Count > 0 || failed.Count > 0)
        {
            if (transaction != null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }

            TempData["WorkInboxError"] = $"عملیات گروهی اتمیک لغو شد. ناموفق: {string.Join("، ", unauthorized.Concat(failed).Take(12))}{(unauthorized.Count + failed.Count > 12 ? " ..." : string.Empty)}";
            return RedirectToIndex(model.Filter);
        }

        if (transaction != null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        TempData["WorkInboxMessage"] = $"{succeeded} مورد با موفقیت انجام شد.";
        if (unauthorized.Count > 0)
        {
            TempData["WorkInboxUnauthorized"] = $"موارد غیرمجاز: {string.Join("، ", unauthorized.Take(12))}{(unauthorized.Count > 12 ? " ..." : string.Empty)}";
        }

        if (failed.Count > 0)
        {
            TempData["WorkInboxError"] = $"موارد ناموفق: {string.Join("، ", failed.Take(12))}{(failed.Count > 12 ? " ..." : string.Empty)}";
        }

        return RedirectToIndex(model.Filter);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddComment(string documentType, int documentId, string body, int? workflowStepId, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Challenge();
        }

        await _workflowService.AddCommentAsync(documentType, documentId, userId, body, workflowStepId, cancellationToken);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delegate(int workflowStepId, string toUserId, string? note, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Challenge();
        }

        await _workflowService.DelegateStepAsync(workflowStepId, userId, toUserId, note, cancellationToken);
        return RedirectToAction(nameof(Index));
    }

    private async Task<BulkItemResult> ApplyWorkflowDecisionAsync(string selectedId, string userId, string decisionType, string? note, CancellationToken cancellationToken)
    {
        if (ParseId(selectedId, "leave-approval-") is int leaveId)
        {
            return await ApplyLeaveDecisionAsync(leaveId, userId, decisionType, note, cancellationToken);
        }

        var stepId = ParseId(selectedId, "workflow-step-");
        if (!stepId.HasValue)
        {
            return BulkItemResult.Failed(selectedId);
        }

        var step = await _context.WorkflowSteps
            .AsNoTracking()
            .Include(item => item.WorkflowInstance)
            .FirstOrDefaultAsync(item => item.Id == stepId.Value, cancellationToken);

        if (step?.WorkflowInstance == null)
        {
            return BulkItemResult.Failed(selectedId);
        }

        var label = $"{step.WorkflowInstance.DocumentType} #{step.WorkflowInstance.DocumentId}";
        var succeeded = await _workflowService.ExecuteDecisionAsync(
            step.WorkflowInstance.DocumentType,
            step.WorkflowInstance.DocumentId,
            step.StepNumber,
            userId,
            decisionType,
            note,
            null,
            null,
            null,
            cancellationToken);

        return succeeded ? BulkItemResult.Success(label) : BulkItemResult.UnauthorizedResult(label);
    }

    private async Task<BulkItemResult> ApplyLeaveDecisionAsync(int leaveId, string userId, string decisionType, string? note, CancellationToken cancellationToken)
    {
        var leave = await _context.Leaves.FirstOrDefaultAsync(item => item.Id == leaveId, cancellationToken);
        if (leave == null)
        {
            return BulkItemResult.Failed($"Leave #{leaveId}");
        }

        if (!await _authorizationFacade.IsSecurityAdminAsync() || leave.Status != WorkflowStatus.PendingApproval)
        {
            return BulkItemResult.UnauthorizedResult($"Leave #{leaveId}");
        }

        var approved = decisionType == WorkflowDecisionType.Approve;
        leave.Status = approved ? WorkflowStatus.Approved : WorkflowStatus.Rejected;
        await _context.SaveChangesAsync(cancellationToken);
        await _workflowService.RecordDecisionAsync(
            nameof(Leave),
            leave.Id,
            1,
            userId,
            leave.Status,
            note,
            cancellationToken);

        return BulkItemResult.Success($"Leave #{leaveId}");
    }

    private async Task<BulkItemResult> DelegateWorkflowItemAsync(string selectedId, string userId, string? toUserId, string? note, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(toUserId))
        {
            return BulkItemResult.Failed("کاربر مقصد انتخاب نشده است.");
        }

        var stepId = ParseId(selectedId, "workflow-step-");
        if (!stepId.HasValue)
        {
            return BulkItemResult.Failed(selectedId);
        }

        var succeeded = await _workflowService.DelegateStepAsync(stepId.Value, userId, toUserId, note, cancellationToken);
        return succeeded ? BulkItemResult.Success(selectedId) : BulkItemResult.UnauthorizedResult(selectedId);
    }

    private async Task<BulkItemResult> SetReadStateAsync(string selectedId, string userId, bool isRead, CancellationToken cancellationToken)
    {
        if (ParseId(selectedId, "workflow-step-") is int stepId)
        {
            var step = await _context.WorkflowSteps.FirstOrDefaultAsync(item => item.Id == stepId, cancellationToken);
            if (step == null)
            {
                return BulkItemResult.Failed(selectedId);
            }

            if (isRead)
            {
                var succeeded = await _workflowService.MarkStepAsReadAsync(stepId, userId, cancellationToken);
                return succeeded ? BulkItemResult.Success(selectedId) : BulkItemResult.UnauthorizedResult(selectedId);
            }

            if (!await CanAccessWorkflowStepAsync(step, userId, cancellationToken))
            {
                return BulkItemResult.UnauthorizedResult(selectedId);
            }

            step.ReadAt = null;
            await _context.SaveChangesAsync(cancellationToken);
            return BulkItemResult.Success(selectedId);
        }

        if (ParseId(selectedId, "letter-unread-") is int letterId)
        {
            var letter = await _context.Letters.FirstOrDefaultAsync(item => item.Id == letterId, cancellationToken);
            if (letter == null)
            {
                return BulkItemResult.Failed(selectedId);
            }

            if (!string.Equals(letter.ReceiverId, userId, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(letter.FinalReceiverId, userId, StringComparison.OrdinalIgnoreCase))
            {
                return BulkItemResult.UnauthorizedResult(selectedId);
            }

            letter.IsRead = isRead;
            letter.ReadDate = isRead ? DateTime.Now : null;
            await _context.SaveChangesAsync(cancellationToken);
            return BulkItemResult.Success(selectedId);
        }

        if (ParseId(selectedId, "notification-") is int notificationId)
        {
            var notification = await _context.Notifications.FirstOrDefaultAsync(item => item.Id == notificationId, cancellationToken);
            if (notification == null)
            {
                return BulkItemResult.Failed(selectedId);
            }

            if (!string.Equals(notification.RecipientUserId, userId, StringComparison.OrdinalIgnoreCase))
            {
                return BulkItemResult.UnauthorizedResult(selectedId);
            }

            notification.IsRead = isRead;
            notification.ReadAt = isRead ? DateTimeOffset.UtcNow : null;
            await _context.SaveChangesAsync(cancellationToken);
            return BulkItemResult.Success(selectedId);
        }

        return BulkItemResult.Failed(selectedId);
    }

    private async Task<bool> CanAccessWorkflowStepAsync(WorkflowStep step, string userId, CancellationToken cancellationToken)
    {
        if (string.Equals(step.AssignedToUserId, userId, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(step.DelegatedFromUserId, userId, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (step.AssignedDepartmentId.HasValue)
        {
            var departmentId = await _context.Users
                .AsNoTracking()
                .Where(item => item.Id == userId)
                .Select(item => item.DepartmentId)
                .FirstOrDefaultAsync(cancellationToken);

            if (departmentId == step.AssignedDepartmentId)
            {
                return true;
            }
        }

        return !string.IsNullOrWhiteSpace(step.AssignedRoleId) &&
               await _identityContext.UserRoles
                   .AsNoTracking()
                   .AnyAsync(item => item.UserId == userId && item.RoleId == step.AssignedRoleId, cancellationToken);
    }

    [HttpGet]
    public async Task<IActionResult> DownloadAttachment(int id, CancellationToken cancellationToken)
    {
        var attachment = await _context.WorkflowAttachments
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (attachment == null)
        {
            return NotFound();
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Challenge();
        }

        var instance = await _context.WorkflowInstances
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == attachment.WorkflowInstanceId, cancellationToken);
        if (instance == null)
        {
            return NotFound();
        }

        var isSecurityAdmin = await _authorizationFacade.IsSecurityAdminAsync();
        var hasStepAccess = false;
        if (!isSecurityAdmin)
        {
            if (string.Equals(instance.StartedByUserId, userId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(instance.CurrentAssigneeUserId, userId, StringComparison.OrdinalIgnoreCase))
            {
                hasStepAccess = true;
            }
            else
            {
                var steps = await _context.WorkflowSteps
                    .AsNoTracking()
                    .Where(s => s.WorkflowInstanceId == instance.Id)
                    .ToListAsync(cancellationToken);

                foreach (var step in steps)
                {
                    if (await CanAccessWorkflowStepAsync(step, userId, cancellationToken))
                    {
                        hasStepAccess = true;
                        break;
                    }
                }

                if (!hasStepAccess)
                {
                    hasStepAccess = await _context.WorkflowDecisions
                        .AsNoTracking()
                        .AnyAsync(d => d.WorkflowInstanceId == instance.Id && d.DecidedByUserId == userId, cancellationToken);
                }
            }
        }
        else
        {
            hasStepAccess = true;
        }

        if (!hasStepAccess)
        {
            return Forbid();
        }

        var physicalPath = _tenantPathResolver != null
            ? _tenantPathResolver.MapRelativeToPhysical(Path.Combine(_environment.ContentRootPath, "App_Data"), attachment.FilePath)
            : Path.Combine(_environment.ContentRootPath, "App_Data", attachment.FilePath.TrimStart('/'));

        if (!System.IO.File.Exists(physicalPath))
        {
            // Fallback for legacy uploads in wwwroot
            physicalPath = _tenantPathResolver != null
                ? _tenantPathResolver.MapRelativeToPhysical(_environment.WebRootPath, attachment.FilePath)
                : Path.Combine(_environment.WebRootPath, attachment.FilePath.TrimStart('/'));

            if (!System.IO.File.Exists(physicalPath))
            {
                return NotFound();
            }
        }

        var bytes = await System.IO.File.ReadAllBytesAsync(physicalPath, cancellationToken);
        return File(bytes, attachment.ContentType ?? "application/octet-stream", attachment.FileName);
    }

    private IActionResult RedirectToIndex(string? filter)
    {
        return string.Equals(filter, "All", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(filter)
            ? RedirectToAction(nameof(Index))
            : RedirectToAction(nameof(Index), new { filter });
    }

    private static int? ParseId(string value, string prefix)
    {
        return value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
               int.TryParse(value[prefix.Length..], out var id)
            ? id
            : null;
    }

    // ── Kanban Board ───────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Kanban(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)) return Challenge();

        var tasks = await _context.WorkflowCaseTasks
            .AsNoTracking()
            .Include(t => t.AssignedToUser)
            .Include(t => t.CreatedByUser)
            .Where(t => t.AssignedToUserId == userId || t.CreatedByUserId == userId)
            .OrderByDescending(t => t.CreatedAt)
            .Take(200)
            .ToListAsync(cancellationToken);

        var taskIds = tasks.Select(t => t.Id).ToList();
        var timeLogs = await _context.TaskTimeLogs
            .AsNoTracking()
            .Where(tl => taskIds.Contains(tl.WorkflowCaseTaskId))
            .ToListAsync(cancellationToken);

        var commentCounts = await _context.WorkflowComments
            .AsNoTracking()
            .Where(c => taskIds.Contains(c.WorkflowInstanceId))
            .GroupBy(c => c.WorkflowInstanceId)
            .Select(g => new { TaskId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var statuses = new[]
        {
            new { Status = "Pending", Title = "در انتظار", ColorClass = "bg-amber-50 border-amber-300" },
            new { Status = "InProgress", Title = "در حال انجام", ColorClass = "bg-blue-50 border-blue-300" },
            new { Status = "NeedsApproval", Title = "نیازمند تایید", ColorClass = "bg-purple-50 border-purple-300" },
            new { Status = "Completed", Title = "تکمیل شده", ColorClass = "bg-emerald-50 border-emerald-300" }
        };

        var columns = new List<KanbanColumnVM>();
        foreach (var statusDef in statuses)
        {
            var columnTasks = tasks.Where(t => NormalizeKanbanStatus(t.Status) == statusDef.Status).ToList();
            var cards = columnTasks.Select(t => new KanbanCardVM
            {
                TaskId = t.Id,
                Title = t.Title,
                Description = t.Description,
                AssignedToName = t.AssignedToUser?.FullName ?? t.AssignedToUser?.UserName,
                Labels = string.IsNullOrWhiteSpace(t.TaskType) ? Array.Empty<string>() : new[] { t.TaskType },
                CommentCount = commentCounts.FirstOrDefault(c => c.TaskId == t.Id)?.Count ?? 0,
                TotalHoursSpent = timeLogs.Where(tl => tl.WorkflowCaseTaskId == t.Id).Sum(tl => tl.HoursSpent),
                CreatedAt = t.CreatedAt
            }).ToList();

            columns.Add(new KanbanColumnVM
            {
                Status = statusDef.Status,
                Title = statusDef.Title,
                ColorClass = statusDef.ColorClass,
                Cards = cards
            });
        }

        var model = new KanbanBoardVM { Columns = columns };
        return View("Kanban", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateTaskStatus(int taskId, string status, CancellationToken cancellationToken)
    {
        var task = await _context.WorkflowCaseTasks.FirstOrDefaultAsync(t => t.Id == taskId, cancellationToken);
        if (task == null) return NotFound(new { success = false, message = "تسک یافت نشد." });

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (task.AssignedToUserId != userId && task.CreatedByUserId != userId)
            return Forbid();

        task.Status = status switch
        {
            "InProgress" => "InProgress",
            "NeedsApproval" => "NeedsApproval",
            "Completed" => WorkflowCaseTaskStatus.Completed,
            _ => "Pending"
        };

        if (task.Status == WorkflowCaseTaskStatus.Completed)
            task.CompletedAt = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return Json(new { success = true });
    }

    // ── Time Tracking ─────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LogTime(int taskId, decimal hoursSpent, string description, CancellationToken cancellationToken)
    {
        if (hoursSpent <= 0 || hoursSpent > 24)
            return BadRequest(new { success = false, message = "مقدار ساعت باید بین ۰.۰۱ تا ۲۴ باشد." });

        if (string.IsNullOrWhiteSpace(description))
            return BadRequest(new { success = false, message = "شرح کارکرد الزامی است." });

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var task = await _context.WorkflowCaseTasks.FirstOrDefaultAsync(t => t.Id == taskId, cancellationToken);
        if (task == null) return NotFound(new { success = false, message = "تسک یافت نشد." });

        _context.TaskTimeLogs.Add(new TaskTimeLog
        {
            WorkflowCaseTaskId = taskId,
            UserId = userId,
            HoursSpent = hoursSpent,
            Description = description.Trim(),
            LoggedAt = DateTime.Now
        });

        await _context.SaveChangesAsync(cancellationToken);
        return Json(new { success = true, message = "کارکرد با موفقیت ثبت شد." });
    }

    [HttpGet]
    public async Task<IActionResult> GetTaskTimeLogs(int taskId, CancellationToken cancellationToken)
    {
        var logs = await _context.TaskTimeLogs
            .AsNoTracking()
            .Include(tl => tl.User)
            .Where(tl => tl.WorkflowCaseTaskId == taskId)
            .OrderByDescending(tl => tl.LoggedAt)
            .Select(tl => new TaskTimeLogItemVM
            {
                Id = tl.Id,
                UserName = tl.User != null ? (tl.User.FullName ?? tl.User.UserName ?? tl.User.Email ?? "کاربر") : "کاربر",
                HoursSpent = tl.HoursSpent,
                Description = tl.Description,
                LoggedAt = tl.LoggedAt
            })
            .ToListAsync(cancellationToken);

        var totalHours = logs.Sum(l => l.HoursSpent);
        return Json(new { items = logs, totalHours });
    }

    // ── @mentions in Comments ──────────────────────────────

    private static readonly Regex MentionRegex = new(@"@([a-zA-Z0-9آ-ی\u0600-\u06FF._@+\- ]{2,50})", RegexOptions.Compiled);

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddCommentWithMentions(AddCommentWithMentionsVM model, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)) return Challenge();

        if (string.IsNullOrWhiteSpace(model.Body))
            return BadRequest(new { success = false, message = "متن کامنت الزامی است." });

        // Extract @mentions
        var mentions = MentionRegex.Matches(model.Body)
            .Select(m => m.Groups[1].Value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Save comment
        var comment = new WorkflowComment
        {
            WorkflowInstanceId = model.DocumentId ?? 0,
            WorkflowStepId = model.WorkflowStepId,
            AuthorUserId = userId,
            Body = model.Body.Trim(),
            Visibility = "Participants",
            CreatedAt = DateTimeOffset.UtcNow
        };
        _context.WorkflowComments.Add(comment);
        await _context.SaveChangesAsync(cancellationToken);

        // Send notifications to mentioned users
        if (mentions.Count > 0)
        {
            var currentUser = await _identityContext.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
            var currentUserName = currentUser?.FullName ?? currentUser?.UserName ?? "کاربر";

            var mentionedUsers = await _identityContext.Users
                .AsNoTracking()
                .Where(u => mentions.Any(m =>
                    (u.FullName != null && u.FullName.Contains(m)) ||
                    (u.UserName != null && u.UserName.Contains(m)) ||
                    (u.Email != null && u.Email.Contains(m))))
                .Take(20)
                .ToListAsync(cancellationToken);

            foreach (var mentioned in mentionedUsers)
            {
                if (mentioned.Id == userId) continue;

                await _notificationService.CreateAsync(
                    mentioned.Id,
                    "منشن در کامنت",
                    $"{currentUserName} شما را در یک کامنت منشن کرده است.",
                    NotificationSeverity.Info,
                    Url.Action(nameof(Index)) ?? "/WorkInbox",
                    cancellationToken: cancellationToken);

                if (!string.IsNullOrWhiteSpace(mentioned.Email))
                {
                    try
                    {
                        await _emailService.SendTaskAssignmentEmailAsync(
                            mentioned.Email,
                            mentioned.FullName ?? mentioned.UserName ?? "کاربر",
                            "منشن در کامنت",
                            currentUserName,
                            Url.Action(nameof(Index), null, Request.Scheme) ?? "/WorkInbox",
                            cancellationToken);
                    }
                    catch { /* email is non-critical */ }
                }
            }
        }

        return Json(new { success = true, mentionCount = mentions.Count });
    }

    private static string NormalizeKanbanStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status)) return "Pending";

        return status switch
        {
            WorkflowCaseTaskStatus.Pending => "Pending",
            WorkflowCaseTaskStatus.Completed => "Completed",
            "InProgress" => "InProgress",
            "NeedsApproval" => "NeedsApproval",
            _ => "Pending"
        };
    }

    private readonly record struct BulkItemResult(bool Succeeded, bool Unauthorized, string Label)
    {
        public static BulkItemResult Success(string label) => new(true, false, label);
        public static BulkItemResult Failed(string label) => new(false, false, label);
        public static BulkItemResult UnauthorizedResult(string label) => new(false, true, label);
    }
}
