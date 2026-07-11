using System.Security.Claims;
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

    public WorkInboxController(
        WorkInboxService workInboxService,
        IAuthorizationFacade authorizationFacade,
        WorkflowService workflowService,
        IWorkflowDbContext context,
        IdentityDbContext identityContext,
        IWebHostEnvironment environment,
        ITenantPathResolver tenantPathResolver)
    {
        _workInboxService = workInboxService;
        _authorizationFacade = authorizationFacade;
        _workflowService = workflowService;
        _context = context;
        _identityContext = identityContext;
        _environment = environment;
        _tenantPathResolver = tenantPathResolver;
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

    private readonly record struct BulkItemResult(bool Succeeded, bool Unauthorized, string Label)
    {
        public static BulkItemResult Success(string label) => new(true, false, label);
        public static BulkItemResult Failed(string label) => new(false, false, label);
        public static BulkItemResult UnauthorizedResult(string label) => new(false, true, label);
    }
}
