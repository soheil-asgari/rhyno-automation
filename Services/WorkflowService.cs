using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Diagnostics;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OfficeAutomation.Models;
using OfficeAutomation.Modules.Identity.Infrastructure.Persistence;
using OfficeAutomation.Modules.Workflow.Infrastructure.Persistence;
using OfficeAutomation.Services.Decisioning;
using OfficeAutomation.Services.Outbox;

namespace OfficeAutomation.Services;

public sealed class WorkflowService
{
    private static readonly JsonSerializerOptions CanonicalJsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    private readonly IWorkflowDbContext? _context;
    private readonly IWebHostEnvironment? _environment;
    private readonly IOutboxService? _outboxService;
    private readonly WorkflowSlaScheduler? _slaScheduler;
    private readonly Tenancy.ITenantPathResolver? _tenantPathResolver;
    private readonly IdentityDbContext? _identityContext;
    private readonly IDecisionEngine _decisionEngine;
    private readonly ILogger<WorkflowService> _logger;
    private readonly IWorkflowDefinitionSelector _definitionSelector;

    public WorkflowService(
        IWorkflowDbContext? context = null,
        IWebHostEnvironment? environment = null,
        IOutboxService? outboxService = null,
        WorkflowSlaScheduler? slaScheduler = null,
        Tenancy.ITenantPathResolver? tenantPathResolver = null,
        IdentityDbContext? identityContext = null,
        IDecisionEngine? decisionEngine = null,
        ILogger<WorkflowService>? logger = null,
        IWorkflowDefinitionSelector? definitionSelector = null)
    {
        _context = context;
        _environment = environment;
        _outboxService = outboxService;
        _slaScheduler = slaScheduler;
        _tenantPathResolver = tenantPathResolver;
        _identityContext = identityContext;
        _decisionEngine = decisionEngine ?? new DecisionEngine();
        _logger = logger ?? NullLogger<WorkflowService>.Instance;
        _definitionSelector = definitionSelector ?? new WorkflowDefinitionSelector();
    }

    public string Normalize(string? status) => WorkflowStatus.Normalize(status);

    public bool IsOpen(string? status)
    {
        var normalized = Normalize(status);
        return normalized != WorkflowStatus.Approved &&
               normalized != WorkflowStatus.Canceled &&
               normalized != WorkflowStatus.Rejected &&
               normalized != WorkflowStatus.Completed;
    }

    public string GetLeaveNextStatus(string currentStatus, bool isApproved)
    {
        if (!isApproved)
        {
            return WorkflowStatus.Rejected;
        }

        if (currentStatus.Contains("ثبت", StringComparison.Ordinal))
        {
            return "در انتظار تایید مدیر واحد";
        }

        if (currentStatus.Contains("مدیر", StringComparison.Ordinal))
        {
            return "در انتظار منابع انسانی";
        }

        if (currentStatus.Contains("منابع", StringComparison.Ordinal))
        {
            return "تایید نهایی";
        }

        return currentStatus;
    }

    public async Task<WorkflowRoutingResult> StartRoutingAsync(
        string documentType,
        string senderId,
        string currentReceiverId,
        int? documentId = null,
        DateTimeOffset? dueAt = null,
        CancellationToken cancellationToken = default)
    {
        EnsureContext();
        var businessData = documentId.HasValue && documentId.Value > 0
            ? await LoadBusinessDataAsync(documentType, documentId.Value, cancellationToken)
            : null;
        var facts = await BuildBusinessFactsAsync(documentType, documentId, senderId, currentReceiverId, businessData, cancellationToken);

        var definition = await GetSelectedDefinitionVersionAsync(documentType, documentId, senderId, cancellationToken);
        var firstStep = definition?.StepDefinitions
            .OrderBy(item => item.StepOrder)
            .FirstOrDefault();

        if (firstStep != null)
        {
            var resolvedStep = await ResolveStepRouteAsync(definition!, firstStep, facts, senderId, cancellationToken);
            if (resolvedStep == null)
            {
                var fallbackManagerId = await GetSenderParentManagerAsync(senderId, cancellationToken);
                if (!string.IsNullOrWhiteSpace(fallbackManagerId))
                {
                    var fallbackPending = WorkflowRoutingResult.Pending(fallbackManagerId, firstStep.StepOrder, firstStep.StepKey);
                    await EnsureStartedInstanceAsync(documentType, documentId, senderId, fallbackPending, dueAt, definition, firstStep, new AssignmentResolution(fallbackManagerId, null, null), null, cancellationToken);
                    return fallbackPending;
                }
            }

            var pending = WorkflowRoutingResult.Pending(
                resolvedStep!.Value.Assignment.UserId ?? currentReceiverId,
                resolvedStep.Value.Step.StepOrder,
                resolvedStep.Value.Step.StepKey);

            await EnsureStartedInstanceAsync(
                documentType,
                documentId,
                senderId,
                pending,
                dueAt,
                definition,
                resolvedStep.Value.Step,
                resolvedStep.Value.Assignment,
                CreateDecisionEnvelope("workflow.start", definition, resolvedStep.Value.RoutingDecision, resolvedStep.Value.AssignmentDecision),
                cancellationToken);

            return pending;
        }

        var senderManagerId = await GetSenderParentManagerAsync(senderId, cancellationToken);
        if (!string.IsNullOrWhiteSpace(senderManagerId))
        {
            var pending = WorkflowRoutingResult.Pending(senderManagerId, 1);
            await EnsureStartedInstanceAsync(documentType, documentId, senderId, pending, dueAt, null, null, new AssignmentResolution(senderManagerId, null, null), null, cancellationToken);
            return pending;
        }

        var completed = WorkflowRoutingResult.Completed(currentReceiverId, WorkflowStatus.Sent, 0);
        await EnsureStartedInstanceAsync(documentType, documentId, senderId, completed, dueAt, null, null, new AssignmentResolution(currentReceiverId, null, null), null, cancellationToken);
        return completed;
    }

    public async Task<WorkflowRoutingResult> AdvanceRoutingAsync(
        string documentType,
        int currentStep,
        string currentReceiverId,
        string? finalReceiverId,
        int? documentId = null,
        string? decidedByUserId = null,
        string? comment = null,
        CancellationToken cancellationToken = default)
    {
        EnsureContext();
        var instance = documentId.HasValue
            ? await RequireInstanceAsync(documentType, documentId.Value, cancellationToken)
            : null;
        var definition = await GetDefinitionVersionForInstanceAsync(documentType, instance, cancellationToken);
        var businessData = documentId.HasValue && documentId.Value > 0
            ? await LoadBusinessDataAsync(documentType, documentId.Value, cancellationToken)
            : null;
        var facts = await BuildBusinessFactsAsync(documentType, documentId, decidedByUserId, currentReceiverId, businessData, cancellationToken);

        WorkflowStepDefinition? currentDefinitionStep = null;
        if (definition != null)
        {
            currentDefinitionStep = await ResolveCurrentDefinitionStepAsync(definition, documentType, documentId, currentStep, cancellationToken);
        }

        RuleRoutingResult? resolvedNext = null;
        if (definition != null)
        {
            resolvedNext = await ResolveNextStepAsync(definition, currentDefinitionStep, facts, decidedByUserId, cancellationToken);
        }

        if (resolvedNext != null)
        {
            var pending = WorkflowRoutingResult.Pending(
                resolvedNext.Value.Assignment.UserId ?? currentReceiverId,
                resolvedNext.Value.Step.StepOrder,
                resolvedNext.Value.Step.StepKey);

            await RecordDecisionAndAdvanceAsync(
                documentType,
                documentId,
                currentStep,
                decidedByUserId,
                WorkflowDecisionType.Approve,
                WorkflowStatus.Approved,
                comment,
                pending,
                definition,
                resolvedNext.Value.Step,
                resolvedNext.Value.Assignment,
                CreateDecisionEnvelope("workflow.advance", definition, resolvedNext.Value.RoutingDecision, resolvedNext.Value.AssignmentDecision),
                cancellationToken);

            return pending;
        }

        var completed = WorkflowRoutingResult.Completed(
            string.IsNullOrWhiteSpace(finalReceiverId) ? currentReceiverId : finalReceiverId,
            WorkflowStatus.Approved,
            Math.Max(1, currentStep));

        await RecordDecisionAndAdvanceAsync(documentType, documentId, currentStep, decidedByUserId, WorkflowDecisionType.Approve, WorkflowStatus.Approved, comment, completed, null, null, new AssignmentResolution(finalReceiverId, null, null), null, cancellationToken);
        return completed;
    }

    public async Task<WorkflowRuleEvaluationResult?> ResolveNextStepAsync(
        string documentType,
        int documentId,
        string? currentStepKey,
        string? actorUserId,
        CancellationToken cancellationToken = default)
    {
        EnsureContext();
        var instance = await RequireInstanceAsync(documentType, documentId, cancellationToken);
        var definition = await GetDefinitionVersionForInstanceAsync(documentType, instance, cancellationToken);
        if (definition == null)
        {
            return null;
        }

        var businessData = await LoadBusinessDataAsync(documentType, documentId, cancellationToken);
        var facts = await BuildBusinessFactsAsync(documentType, documentId, actorUserId, null, businessData, cancellationToken);
        var currentStep = string.IsNullOrWhiteSpace(currentStepKey)
            ? definition.StepDefinitions.OrderBy(item => item.StepOrder).FirstOrDefault()
            : definition.StepDefinitions.FirstOrDefault(item => item.StepKey == currentStepKey);

        var resolved = await ResolveNextStepAsync(definition, currentStep, facts, actorUserId, cancellationToken);
        return resolved == null
            ? null
            : new WorkflowRuleEvaluationResult
            {
                DefinitionVersionId = definition.Id,
                CurrentStepKey = currentStep?.StepKey,
                NextStepKey = resolved.Value.Step.StepKey,
                NextStepOrder = resolved.Value.Step.StepOrder,
                AssigneeUserId = resolved.Value.Assignment.UserId,
                AssigneeRoleId = resolved.Value.Assignment.RoleId,
                AssigneeDepartmentId = resolved.Value.Assignment.DepartmentId,
                Facts = facts,
                DefinitionVersion = definition.Version,
                Explanation = CreateDecisionEnvelope("workflow.preview", definition, resolved.Value.RoutingDecision, resolved.Value.AssignmentDecision)
            };
    }

    public async Task RecordDecisionAsync(
        string documentType,
        int documentId,
        int stepNumber,
        string decidedByUserId,
        string decision,
        string? comment = null,
        CancellationToken cancellationToken = default)
    {
        await ExecuteDecisionAsync(documentType, documentId, stepNumber, decidedByUserId, WorkflowDecisionType.Comment, comment ?? decision, null, null, null, cancellationToken);
    }

    public async Task StartDirectAssignmentAsync(
        string documentType,
        int documentId,
        string startedByUserId,
        string assignedToUserId,
        DateTimeOffset? dueAt = null,
        CancellationToken cancellationToken = default)
    {
        var routing = WorkflowRoutingResult.Pending(assignedToUserId, 1);
        await EnsureStartedInstanceAsync(documentType, documentId, startedByUserId, routing, dueAt, null, null, new AssignmentResolution(assignedToUserId, null, null), null, cancellationToken);
    }

    public async Task<WorkflowCaseTask?> CreateAdHocTaskAsync(
        string documentType,
        int documentId,
        int workflowStepId,
        string createdByUserId,
        string assignedToUserId,
        string title,
        string? description = null,
        CancellationToken cancellationToken = default)
    {
        EnsureContext();
        var instance = await _context!.WorkflowInstances
            .Include(item => item.Steps)
            .FirstOrDefaultAsync(item => item.DocumentType == documentType && item.DocumentId == documentId, cancellationToken);
        if (instance == null)
        {
            return null;
        }

        var step = instance.Steps.FirstOrDefault(item => item.Id == workflowStepId && item.CompletedAt == null);
        if (step == null)
        {
            return null;
        }

        var task = new WorkflowCaseTask
        {
            WorkflowInstanceId = instance.Id,
            WorkflowStepId = step.Id,
            TaskType = WorkflowCaseTaskType.AdHoc,
            Title = title.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            CreatedByUserId = createdByUserId,
            AssignedToUserId = assignedToUserId
        };

        _context.WorkflowCaseTasks.Add(task);
        await AddActionLogAsync(instance.Id, step.Id, createdByUserId, WorkflowDecisionType.AdHocAssign, title, JsonSerializer.Serialize(new { assignedToUserId, description }), cancellationToken);
        await RecordTransitionEventAsync(instance, step, createdByUserId, "case_task.created", instance.Status, instance.Status, step.StepNumber, step.StepNumber, step.StepKey, step.StepName, JsonSerializer.Serialize(new { taskType = WorkflowCaseTaskType.AdHoc, task.Title }), cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return task;
    }

    public async Task<WorkflowInstance?> CreateSubCaseAsync(
        string parentDocumentType,
        int parentDocumentId,
        int workflowStepId,
        string createdByUserId,
        string subCaseDocumentType,
        int subCaseDocumentId,
        string assignedToUserId,
        string title,
        string? description = null,
        CancellationToken cancellationToken = default)
    {
        EnsureContext();
        var parent = await _context!.WorkflowInstances
            .Include(item => item.Steps)
            .FirstOrDefaultAsync(item => item.DocumentType == parentDocumentType && item.DocumentId == parentDocumentId, cancellationToken);
        if (parent == null)
        {
            return null;
        }

        var step = parent.Steps.FirstOrDefault(item => item.Id == workflowStepId && item.CompletedAt == null);
        if (step == null)
        {
            return null;
        }

        var subRouting = WorkflowRoutingResult.Pending(assignedToUserId, 1, "subcase-start");
        await EnsureStartedInstanceAsync(subCaseDocumentType, subCaseDocumentId, createdByUserId, subRouting, null, null, null, new AssignmentResolution(assignedToUserId, null, null), null, cancellationToken);

        var subCase = await _context.WorkflowInstances
            .Include(item => item.Steps)
            .FirstAsync(item => item.DocumentType == subCaseDocumentType && item.DocumentId == subCaseDocumentId, cancellationToken);

        subCase.ParentWorkflowInstanceId = parent.Id;
        parent.Status = WorkflowStatus.Paused;
        parent.CurrentStatus = WorkflowStatus.Paused;

        var caseTask = new WorkflowCaseTask
        {
            WorkflowInstanceId = parent.Id,
            WorkflowStepId = step.Id,
            TaskType = WorkflowCaseTaskType.SubCase,
            Title = title.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            CreatedByUserId = createdByUserId,
            AssignedToUserId = assignedToUserId,
            SubCaseInstanceId = subCase.Id
        };

        _context.WorkflowCaseTasks.Add(caseTask);
        await AddActionLogAsync(parent.Id, step.Id, createdByUserId, WorkflowDecisionType.CreateSubCase, title, JsonSerializer.Serialize(new { subCaseDocumentType, subCaseDocumentId }), cancellationToken);
        await RecordTransitionEventAsync(parent, step, createdByUserId, "workflow.paused", WorkflowStatus.PendingApproval, WorkflowStatus.Paused, step.StepNumber, step.StepNumber, step.StepKey, step.StepName, JsonSerializer.Serialize(new { subCase.Id, task = caseTask.Title }), cancellationToken);
        await RecordTransitionEventAsync(subCase, subCase.Steps.FirstOrDefault(), createdByUserId, "subcase.created", null, subCase.Status, null, subCase.CurrentStepNumber, "subcase-start", title, JsonSerializer.Serialize(new { parentId = parent.Id }), cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return subCase;
    }

    public async Task<bool> CompleteCaseTaskAsync(int caseTaskId, string actorUserId, string? note = null, CancellationToken cancellationToken = default)
    {
        EnsureContext();
        var caseTask = await _context!.WorkflowCaseTasks
            .Include(item => item.WorkflowInstance)
            .Include(item => item.WorkflowStep)
            .Include(item => item.SubCaseInstance)
            .FirstOrDefaultAsync(item => item.Id == caseTaskId, cancellationToken);
        if (caseTask?.WorkflowInstance == null || caseTask.Status != WorkflowCaseTaskStatus.Pending)
        {
            return false;
        }

        caseTask.Status = WorkflowCaseTaskStatus.Completed;
        caseTask.CompletedAt = DateTimeOffset.UtcNow;

        if (caseTask.TaskType == WorkflowCaseTaskType.SubCase &&
            caseTask.SubCaseInstance != null &&
            WorkflowStatus.Normalize(caseTask.SubCaseInstance.Status) != WorkflowStatus.Approved &&
            WorkflowStatus.Normalize(caseTask.SubCaseInstance.Status) != WorkflowStatus.Completed)
        {
            caseTask.SubCaseInstance.Status = WorkflowStatus.Completed;
            caseTask.SubCaseInstance.CurrentStatus = WorkflowStatus.Completed;
            caseTask.SubCaseInstance.CompletedAt = DateTimeOffset.UtcNow;
            caseTask.SubCaseInstance.ClosedAt = DateTimeOffset.UtcNow;
        }

        var parent = caseTask.WorkflowInstance;
        var openSubCases = await _context.WorkflowCaseTasks
            .CountAsync(item => item.WorkflowInstanceId == parent.Id && item.TaskType == WorkflowCaseTaskType.SubCase && item.Status == WorkflowCaseTaskStatus.Pending, cancellationToken);

        if (openSubCases == 1 && caseTask.TaskType == WorkflowCaseTaskType.SubCase)
        {
            parent.Status = WorkflowStatus.PendingApproval;
            parent.CurrentStatus = WorkflowStatus.PendingApproval;
            await RecordTransitionEventAsync(parent, caseTask.WorkflowStep, actorUserId, "workflow.resumed", WorkflowStatus.Paused, WorkflowStatus.PendingApproval, parent.CurrentStepNumber, parent.CurrentStepNumber, caseTask.WorkflowStep?.StepKey, caseTask.WorkflowStep?.StepName, note, cancellationToken);
        }

        await AddActionLogAsync(parent.Id, caseTask.WorkflowStepId, actorUserId, caseTask.TaskType == WorkflowCaseTaskType.SubCase ? WorkflowDecisionType.CompleteSubCase : WorkflowDecisionType.AdHocAssign, note ?? caseTask.Title, JsonSerializer.Serialize(new { caseTaskId }), cancellationToken);
        await RecordTransitionEventAsync(parent, caseTask.WorkflowStep, actorUserId, "case_task.completed", parent.Status, parent.Status, parent.CurrentStepNumber, parent.CurrentStepNumber, caseTask.WorkflowStep?.StepKey, caseTask.WorkflowStep?.StepName, JsonSerializer.Serialize(new { caseTaskId, caseTask.TaskType }), cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<WorkflowInstance?> GetInstanceAsync(string documentType, int documentId, CancellationToken cancellationToken = default)
    {
        EnsureContext();
        return await _context!.WorkflowInstances
            .Include(item => item.Steps)
            .Include(item => item.Decisions)
            .Include(item => item.ActionLogs)
            .Include(item => item.Comments)
            .Include(item => item.Attachments)
            .FirstOrDefaultAsync(item => item.DocumentType == documentType && item.DocumentId == documentId, cancellationToken);
    }

    public async Task<List<WorkflowActionLog>> GetTimelineAsync(string documentType, int documentId, CancellationToken cancellationToken = default)
    {
        EnsureContext();
        return await _context!.WorkflowActionLogs
            .AsNoTracking()
            .Include(item => item.ActorUser)
            .Include(item => item.WorkflowInstance)
            .Where(item => item.WorkflowInstance != null &&
                           item.WorkflowInstance.DocumentType == documentType &&
                           item.WorkflowInstance.DocumentId == documentId)
            .OrderByDescending(item => item.OccurredAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<WorkflowComment?> AddCommentAsync(
        string documentType,
        int documentId,
        string authorUserId,
        string body,
        int? workflowStepId = null,
        CancellationToken cancellationToken = default)
    {
        EnsureContext();
        var instance = await RequireInstanceAsync(documentType, documentId, cancellationToken);
        if (instance == null || string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        var comment = new WorkflowComment
        {
            WorkflowInstanceId = instance.Id,
            WorkflowStepId = workflowStepId,
            AuthorUserId = authorUserId,
            Body = body.Trim()
        };

        _context!.WorkflowComments.Add(comment);
        instance.LastActionAt = DateTimeOffset.UtcNow;

        await AddActionLogAsync(instance.Id, workflowStepId, authorUserId, WorkflowDecisionType.Comment, comment.Body, null, cancellationToken);
        await RecordTransitionEventAsync(instance, null, authorUserId, "comment.added", instance.Status, instance.Status, instance.CurrentStepNumber, instance.CurrentStepNumber, null, null, comment.Body, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return comment;
    }

    public async Task<WorkflowAttachment?> AddAttachmentAsync(
        string documentType,
        int documentId,
        string userId,
        IFormFile file,
        int? workflowStepId = null,
        int? workflowDecisionId = null,
        CancellationToken cancellationToken = default)
    {
        EnsureContext();
        if (_environment == null || file == null || file.Length == 0)
        {
            return null;
        }

        var instance = await RequireInstanceAsync(documentType, documentId, cancellationToken);
        if (instance == null)
        {
            return null;
        }

        var uploadsRoot = _tenantPathResolver?.GetWorkflowUploadRoot(_environment.WebRootPath, documentType, documentId)
            ?? Path.Combine(_environment.WebRootPath, "uploads", "workflow", documentType.ToLowerInvariant(), documentId.ToString(CultureInfo.InvariantCulture));
        Directory.CreateDirectory(uploadsRoot);

        var safeFileName = $"{Guid.NewGuid():N}-{Path.GetFileName(file.FileName)}";
        var physicalPath = Path.Combine(uploadsRoot, safeFileName);

        await using (var stream = File.Create(physicalPath))
        {
            await file.CopyToAsync(stream, cancellationToken);
        }

        var relativePath = _tenantPathResolver?.GetTenantRelativePath("uploads", "workflow", documentType.ToLowerInvariant(), documentId.ToString(CultureInfo.InvariantCulture), safeFileName)
            ?? $"/uploads/workflow/{documentType.ToLowerInvariant()}/{documentId.ToString(CultureInfo.InvariantCulture)}/{safeFileName}";
        var attachment = new WorkflowAttachment
        {
            WorkflowInstanceId = instance.Id,
            WorkflowStepId = workflowStepId,
            WorkflowDecisionId = workflowDecisionId,
            FileName = Path.GetFileName(file.FileName),
            FilePath = relativePath,
            ContentType = file.ContentType ?? "application/octet-stream",
            FileSize = file.Length,
            UploadedByUserId = userId
        };

        _context!.WorkflowAttachments.Add(attachment);
        instance.LastActionAt = DateTimeOffset.UtcNow;
        await AddActionLogAsync(instance.Id, workflowStepId, userId, "Attachment", attachment.FileName, JsonSerializer.Serialize(new { attachment.FilePath }), cancellationToken);
        await RecordTransitionEventAsync(instance, null, userId, "attachment.added", instance.Status, instance.Status, instance.CurrentStepNumber, instance.CurrentStepNumber, null, null, attachment.FileName, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return attachment;
    }

    public async Task<bool> MarkStepAsReadAsync(int workflowStepId, string userId, CancellationToken cancellationToken = default)
    {
        EnsureContext();
        var step = await _context!.WorkflowSteps
            .Include(item => item.WorkflowInstance)
            .FirstOrDefaultAsync(item => item.Id == workflowStepId, cancellationToken);
        if (step == null || !await IsAssignedToUserAsync(step, userId, cancellationToken))
        {
            return false;
        }

        if (step.ReadAt == null)
        {
            step.ReadAt = DateTimeOffset.UtcNow;
            if (step.WorkflowInstance != null)
            {
                step.WorkflowInstance.LastActionAt = DateTimeOffset.UtcNow;
                await AddActionLogAsync(step.WorkflowInstance.Id, step.Id, userId, "Read", "آیتم مشاهده شد.", null, cancellationToken);
            }
            await _context.SaveChangesAsync(cancellationToken);
        }

        return true;
    }

    public async Task<bool> DelegateStepAsync(int workflowStepId, string fromUserId, string toUserId, string? note, CancellationToken cancellationToken = default)
    {
        EnsureContext();
        var step = await _context!.WorkflowSteps
            .Include(item => item.WorkflowInstance)
            .FirstOrDefaultAsync(item => item.Id == workflowStepId, cancellationToken);
        if (step == null || step.WorkflowInstance == null || !await IsAssignedToUserAsync(step, fromUserId, cancellationToken))
        {
            return false;
        }

        step.DelegatedFromUserId = fromUserId;
        step.AssignedToUserId = toUserId;
        step.AssignmentMode = WorkflowAssignmentMode.User;
        step.ReadAt = null;
        step.WorkflowInstance.CurrentAssigneeUserId = toUserId;
        step.WorkflowInstance.CurrentAssigneeRoleId = null;
        step.WorkflowInstance.CurrentAssigneeDepartmentId = null;
        step.WorkflowInstance.LastActionAt = DateTimeOffset.UtcNow;
        step.SlaState = WorkflowSlaState.OnTrack;
        step.WorkflowInstance.SlaState = WorkflowSlaState.OnTrack;

        _context.WorkflowDelegations.Add(new WorkflowDelegation
        {
            FromUserId = fromUserId,
            ToUserId = toUserId,
            DocumentType = step.WorkflowInstance.DocumentType,
            StartsAt = DateTimeOffset.UtcNow,
            EndsAt = step.DueAt ?? DateTimeOffset.UtcNow.AddDays(7),
            IsActive = true
        });

        _context.WorkflowDecisions.Add(new WorkflowDecision
        {
            WorkflowInstanceId = step.WorkflowInstance.Id,
            WorkflowStepId = step.Id,
            DecidedByUserId = fromUserId,
            Decision = WorkflowStatus.PendingApproval,
            DecisionType = WorkflowDecisionType.Delegate,
            Comment = note
        });

        await AddActionLogAsync(step.WorkflowInstance.Id, step.Id, fromUserId, WorkflowDecisionType.Delegate, note ?? $"ارجاع به {toUserId}", JsonSerializer.Serialize(new { toUserId }), cancellationToken);
        await RecordTransitionEventAsync(step.WorkflowInstance, step, fromUserId, "step.delegated", step.WorkflowInstance.Status, step.WorkflowInstance.Status, step.StepNumber, step.StepNumber, step.StepKey, step.StepName, JsonSerializer.Serialize(new { toUserId }), cancellationToken);
        if (_slaScheduler != null)
        {
            await _slaScheduler.CancelStepJobsAsync(step.Id, "Step delegated to another user.", cancellationToken);
            await _slaScheduler.ScheduleStepAsync(step.WorkflowInstance, step, ResolveSlaHours(step), cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> ExecuteDecisionAsync(
        string documentType,
        int documentId,
        int stepNumber,
        string userId,
        string decisionType,
        string? note,
        string? signatureText,
        IEnumerable<IFormFile>? attachments,
        string? forwardToUserId,
        CancellationToken cancellationToken = default)
    {
        EnsureContext();
        var instance = await _context!.WorkflowInstances
            .Include(item => item.Steps)
            .Include(item => item.Decisions)
            .FirstOrDefaultAsync(item => item.DocumentType == documentType && item.DocumentId == documentId, cancellationToken);

        if (instance == null)
        {
            return false;
        }

        if (WorkflowStatus.Normalize(instance.Status) == WorkflowStatus.Paused)
        {
            return false;
        }

        try
        {
        var step = instance.Steps
            .OrderByDescending(item => item.StepNumber)
            .FirstOrDefault(item => item.StepNumber == stepNumber && item.CompletedAt == null);

        if (step == null || !await IsAssignedToUserAsync(step, userId, cancellationToken))
        {
            return false;
        }

        var normalizedType = decisionType?.Trim() ?? WorkflowDecisionType.Comment;
        var previousStepStatus = step.Status;
        var decisionStatus = normalizedType switch
        {
            WorkflowDecisionType.Approve => WorkflowStatus.Approved,
            WorkflowDecisionType.Reject => WorkflowStatus.Rejected,
            WorkflowDecisionType.Return => WorkflowStatus.Returned,
            WorkflowDecisionType.RequestChanges => WorkflowStatus.NeedsRevision,
            WorkflowDecisionType.Forward => WorkflowStatus.PendingApproval,
            WorkflowDecisionType.Delegate => WorkflowStatus.PendingApproval,
            _ => step.Status
        };

        step.Status = decisionStatus;
        step.CompletedAt = normalizedType == WorkflowDecisionType.Forward || normalizedType == WorkflowDecisionType.Delegate ? null : DateTimeOffset.UtcNow;
        step.SlaState = WorkflowSlaState.OnTrack;
        instance.LastActionAt = DateTimeOffset.UtcNow;

        if (step.CompletedAt != null && _slaScheduler != null)
        {
            await _slaScheduler.CancelStepJobsAsync(step.Id, $"Step closed by {normalizedType}.", cancellationToken);
        }

        var decision = new WorkflowDecision
        {
            WorkflowInstanceId = instance.Id,
            WorkflowStepId = step.Id,
            DecidedByUserId = userId,
            Decision = decisionStatus,
            DecisionType = normalizedType,
            Comment = string.IsNullOrWhiteSpace(note) ? null : note.Trim()
        };

        _context.WorkflowDecisions.Add(decision);
        await _context.SaveChangesAsync(cancellationToken);

        if (attachments != null)
        {
            foreach (var file in attachments.Where(item => item.Length > 0))
            {
                var attachment = await AddAttachmentAsync(documentType, documentId, userId, file, step.Id, decision.Id, cancellationToken);
                if (attachment != null)
                {
                    decision.AttachmentCount++;
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(signatureText))
        {
            await CreateDocumentSignatureAsync(instance, decision, userId, signatureText, note, cancellationToken);
        }

        switch (normalizedType)
        {
            case WorkflowDecisionType.Approve:
                {
                    var next = await AdvanceRoutingAsync(documentType, stepNumber, userId, instance.StartedByUserId, documentId, userId, note, cancellationToken);
                    instance.Status = next.Status;
                    instance.CurrentStatus = next.Status;
                    break;
                }
            case WorkflowDecisionType.Reject:
                instance.Status = WorkflowStatus.Rejected;
                instance.CurrentStatus = WorkflowStatus.Rejected;
                instance.ClosedAt = DateTimeOffset.UtcNow;
                instance.CompletedAt = DateTimeOffset.UtcNow;
                break;
            case WorkflowDecisionType.RequestChanges:
                instance.Status = WorkflowStatus.NeedsRevision;
                instance.CurrentStatus = WorkflowStatus.NeedsRevision;
                instance.CurrentAssigneeUserId = instance.StartedByUserId;
                break;
            case WorkflowDecisionType.Return:
                await ReturnToPreviousStepAsync(instance, step, userId, note, cancellationToken);
                break;
            case WorkflowDecisionType.Forward:
                await ForwardToUserAsync(instance, step, userId, forwardToUserId, note, cancellationToken);
                break;
        }

        if (normalizedType != WorkflowDecisionType.Approve)
        {
            EnqueueWorkflowStatusEvent(
                instance,
                step,
                userId,
                normalizedType,
                previousStepStatus,
                step.Status,
                note);
        }

        await AddActionLogAsync(instance.Id, step.Id, userId, normalizedType, note, signatureText, cancellationToken);
        await RecordTransitionEventAsync(instance, step, userId, $"workflow.{normalizedType.ToLowerInvariant()}", previousStepStatus, instance.Status, step.StepNumber, instance.CurrentStepNumber, step.StepKey, step.StepName, note, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await MarkIncidentAsync(instance, null, userId, ex, cancellationToken);
            return false;
        }
    }

    public async Task<bool> RetryIncidentAsync(int workflowIncidentId, string actorUserId, string? resolutionNote = null, CancellationToken cancellationToken = default)
    {
        EnsureContext();
        var incident = await _context!.WorkflowIncidents
            .Include(item => item.WorkflowInstance)
            .FirstOrDefaultAsync(item => item.Id == workflowIncidentId, cancellationToken);
        if (incident?.WorkflowInstance == null || incident.IsResolved)
        {
            return false;
        }

        incident.IsResolved = true;
        incident.ResolvedAt = DateTimeOffset.UtcNow;
        incident.RetriedAt = DateTimeOffset.UtcNow;
        incident.ResolvedByUserId = actorUserId;
        incident.ResolutionNote = string.IsNullOrWhiteSpace(resolutionNote) ? "Incident marked for retry." : resolutionNote.Trim();
        incident.WorkflowInstance.Status = WorkflowStatus.PendingApproval;
        incident.WorkflowInstance.CurrentStatus = WorkflowStatus.PendingApproval;
        incident.WorkflowInstance.LastActionAt = DateTimeOffset.UtcNow;

        await AddActionLogAsync(incident.WorkflowInstanceId, incident.WorkflowStepId, actorUserId, "incident.retry", incident.ResolutionNote, null, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public Task<int> RefreshSlaStatesAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(0);
    }

    private async Task<string?> GetSenderParentManagerAsync(string senderId, CancellationToken cancellationToken)
    {
        EnsureContext();
        return await _context!.Users
            .AsNoTracking()
            .Where(item => item.Id == senderId)
            .Select(item => item.ParentManagerUserId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task EnsureStartedInstanceAsync(
        string documentType,
        int? documentId,
        string? startedByUserId,
        WorkflowRoutingResult routing,
        DateTimeOffset? dueAt,
        WorkflowDefinitionVersion? definition,
        WorkflowStepDefinition? stepDefinition,
        AssignmentResolution assignment,
        DecisionExplanationEnvelope? explanation,
        CancellationToken cancellationToken)
    {
        if (!documentId.HasValue || documentId.Value <= 0 || _context == null)
        {
            return;
        }

        var exists = await _context.WorkflowInstances
            .AnyAsync(item => item.DocumentType == documentType && item.DocumentId == documentId.Value, cancellationToken);
        if (exists)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;

        var instance = new WorkflowInstance
        {
            DefinitionVersionId = definition?.Id,
            DocumentType = documentType,
            DocumentId = documentId.Value,
            Status = routing.Status,
            CurrentStatus = routing.Status,
            CurrentStepNumber = routing.StepNumber,
            StartedByUserId = string.IsNullOrWhiteSpace(startedByUserId) ? null : startedByUserId,
            DueAt = dueAt,
            CompletedAt = routing.IsCompleted ? now : null,
            ClosedAt = routing.IsCompleted ? now : null,
            SlaState = WorkflowSlaState.OnTrack,
            CurrentAssigneeUserId = assignment.UserId,
            CurrentAssigneeRoleId = assignment.RoleId,
            CurrentAssigneeDepartmentId = assignment.DepartmentId,
            LastActionAt = now
        };

        if (!routing.IsCompleted && routing.StepNumber > 0)
        {
            instance.Steps.Add(new WorkflowStep
            {
                StepNumber = routing.StepNumber,
                StepKey = stepDefinition?.StepKey,
                StepName = stepDefinition?.StepKey ?? $"Step {routing.StepNumber}",
                AssignmentMode = stepDefinition?.AssignmentMode ?? WorkflowAssignmentMode.User,
                AssignedToUserId = assignment.UserId,
                AssignedRoleId = assignment.RoleId,
                AssignedDepartmentId = assignment.DepartmentId,
                Status = WorkflowStatus.PendingApproval,
                SlaState = WorkflowSlaState.OnTrack
            });
        }

        _context.WorkflowInstances.Add(instance);

        await _context.SaveChangesAsync(cancellationToken);

        if (!routing.IsCompleted && instance.Steps.Count > 0 && _slaScheduler != null)
        {
            await _slaScheduler.ScheduleStepAsync(instance, instance.Steps[0], ResolveSlaHours(stepDefinition), cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(startedByUserId))
        {
            var metadataJson = explanation == null ? null : JsonSerializer.Serialize(explanation);
            await AddActionLogAsync(instance.Id, instance.Steps.FirstOrDefault()?.Id, startedByUserId, WorkflowDecisionType.Start, $"شروع گردش کار {documentType}", metadataJson, cancellationToken);
            await RecordTransitionEventAsync(instance, instance.Steps.FirstOrDefault(), startedByUserId, "workflow.started", null, instance.Status, null, instance.CurrentStepNumber, stepDefinition?.StepKey, stepDefinition?.StepKey, metadataJson, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task RecordDecisionAndAdvanceAsync(
        string documentType,
        int? documentId,
        int currentStep,
        string? decidedByUserId,
        string decisionType,
        string decision,
        string? comment,
        WorkflowRoutingResult nextRouting,
        WorkflowDefinitionVersion? definition,
        WorkflowStepDefinition? stepDefinition,
        AssignmentResolution assignment,
        DecisionExplanationEnvelope? explanation,
        CancellationToken cancellationToken)
    {
        if (!documentId.HasValue || string.IsNullOrWhiteSpace(decidedByUserId) || _context == null)
        {
            return;
        }

        var instance = await _context.WorkflowInstances
            .Include(item => item.Steps)
            .FirstOrDefaultAsync(item => item.DocumentType == documentType && item.DocumentId == documentId.Value, cancellationToken);
        if (instance == null)
        {
            return;
        }

        var step = instance.Steps
            .OrderByDescending(item => item.StepNumber)
            .FirstOrDefault(item => item.StepNumber == currentStep);
        if (step != null)
        {
            step.Status = WorkflowStatus.Normalize(decision);
            step.CompletedAt = DateTimeOffset.UtcNow;
            if (_slaScheduler != null)
            {
                await _slaScheduler.CancelStepJobsAsync(step.Id, $"Step advanced by {decisionType}.", cancellationToken);
            }
        }

        _context.WorkflowDecisions.Add(new WorkflowDecision
        {
            WorkflowInstanceId = instance.Id,
            WorkflowStepId = step?.Id,
            DecidedByUserId = decidedByUserId,
            Decision = WorkflowStatus.Normalize(decision),
            DecisionType = decisionType,
            Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim()
        });

        var previousStatus = instance.Status;
        instance.Status = nextRouting.Status;
        instance.CurrentStatus = nextRouting.Status;
        instance.CurrentStepNumber = nextRouting.StepNumber;
        instance.DefinitionVersionId = definition?.Id ?? instance.DefinitionVersionId;
        instance.CompletedAt = nextRouting.IsCompleted ? DateTimeOffset.UtcNow : null;
        instance.ClosedAt = nextRouting.IsCompleted ? DateTimeOffset.UtcNow : null;
        instance.CurrentAssigneeUserId = assignment.UserId;
        instance.CurrentAssigneeRoleId = assignment.RoleId;
        instance.CurrentAssigneeDepartmentId = assignment.DepartmentId;
        instance.LastActionAt = DateTimeOffset.UtcNow;

        if (!nextRouting.IsCompleted && nextRouting.StepNumber > currentStep)
        {
            instance.Steps.Add(new WorkflowStep
            {
                StepNumber = nextRouting.StepNumber,
                StepKey = nextRouting.StepKey ?? stepDefinition?.StepKey,
                StepName = stepDefinition?.StepKey ?? $"Step {nextRouting.StepNumber}",
                AssignmentMode = stepDefinition?.AssignmentMode ?? WorkflowAssignmentMode.User,
                AssignedToUserId = assignment.UserId,
                AssignedRoleId = assignment.RoleId,
                AssignedDepartmentId = assignment.DepartmentId,
                Status = WorkflowStatus.PendingApproval,
                SlaState = WorkflowSlaState.OnTrack
            });
            instance.SlaState = WorkflowSlaState.OnTrack;
        }

        EnqueueWorkflowStatusEvent(
            instance,
            step,
            decidedByUserId,
            decisionType,
            previousStatus,
            instance.Status,
            comment);

        await _context.SaveChangesAsync(cancellationToken);

        if (!nextRouting.IsCompleted && nextRouting.StepNumber > currentStep && _slaScheduler != null)
        {
            var scheduledStep = instance.Steps.OrderByDescending(item => item.Id).First(item => item.StepNumber == nextRouting.StepNumber);
            await _slaScheduler.ScheduleStepAsync(instance, scheduledStep, ResolveSlaHours(stepDefinition), cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
        }

        await RecordTransitionEventAsync(
            instance,
            instance.Steps.OrderByDescending(item => item.Id).FirstOrDefault(item => item.StepNumber == instance.CurrentStepNumber),
            decidedByUserId,
            "workflow.transition",
            previousStatus,
            instance.Status,
            currentStep,
            instance.CurrentStepNumber,
            nextRouting.StepKey ?? stepDefinition?.StepKey,
            nextRouting.StepKey ?? stepDefinition?.StepKey,
            explanation == null ? comment : JsonSerializer.Serialize(explanation),
            cancellationToken);
    }

    private async Task<WorkflowDefinitionVersion?> GetActiveDefinitionVersionAsync(string documentType, CancellationToken cancellationToken)
    {
        EnsureContext();
        var now = DateTimeOffset.UtcNow;
        return await QueryDefinitionVersions()
            .AsNoTracking()
            .Where(item =>
                item.DocumentType == documentType &&
                item.IsActive &&
                item.EffectiveFrom <= now &&
                (item.EffectiveTo == null || item.EffectiveTo >= now))
            .OrderByDescending(item => item.Version)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<WorkflowDefinitionVersion?> GetSelectedDefinitionVersionAsync(
        string documentType,
        int? documentId,
        string? startedByUserId,
        CancellationToken cancellationToken)
    {
        EnsureContext();
        var now = DateTimeOffset.UtcNow;
        var candidates = await QueryDefinitionVersions()
            .AsNoTracking()
            .Where(item =>
                item.DocumentType == documentType &&
                item.IsActive &&
                item.EffectiveFrom <= now &&
                (item.EffectiveTo == null || item.EffectiveTo >= now))
            .OrderByDescending(item => item.Version)
            .ToListAsync(cancellationToken);

        return _definitionSelector.SelectVersion(documentType, candidates, documentId, startedByUserId);
    }

    private async Task<WorkflowDefinitionVersion?> GetDefinitionVersionForInstanceAsync(
        string documentType,
        WorkflowInstance? instance,
        CancellationToken cancellationToken)
    {
        if (instance?.DefinitionVersionId is int versionId)
        {
            var boundVersion = await QueryDefinitionVersions()
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == versionId, cancellationToken);
            if (boundVersion != null)
            {
                return boundVersion;
            }
        }

        return await GetActiveDefinitionVersionAsync(documentType, cancellationToken);
    }

    private IQueryable<WorkflowDefinitionVersion> QueryDefinitionVersions()
    {
        return _context!.WorkflowDefinitionVersions
            .Include(item => item.StepDefinitions.OrderBy(step => step.StepOrder))
                .ThenInclude(item => item.Rules);
    }

    private async Task<RuleRoutingResult?> ResolveStepRouteAsync(
        WorkflowDefinitionVersion definition,
        WorkflowStepDefinition stepDefinition,
        IReadOnlyDictionary<string, object?> facts,
        string? actorUserId,
        CancellationToken cancellationToken)
    {
        var assignmentDecision = EvaluateDecisionTable(definition, stepDefinition, facts, "assignment");
        var assignment = await ResolveAssignmentAsync(stepDefinition.AssignmentMode, assignmentDecision, definition.DocumentType, actorUserId, cancellationToken);
        if (!assignment.HasValue)
        {
            return null;
        }

        return new RuleRoutingResult(stepDefinition, assignment.Value, null, assignmentDecision);
    }

    private async Task<RuleRoutingResult?> ResolveNextStepAsync(
        WorkflowDefinitionVersion definition,
        WorkflowStepDefinition? currentStep,
        IReadOnlyDictionary<string, object?> facts,
        string? actorUserId,
        CancellationToken cancellationToken)
    {
        if (currentStep == null)
        {
            return null;
        }

        var routingDecision = EvaluateDecisionTable(definition, currentStep, facts, "routing");
        WorkflowStepDefinition? nextStep = null;

        if (!string.IsNullOrWhiteSpace(GetOutputValue(routingDecision, "NextStepKey")))
        {
            nextStep = definition.StepDefinitions
                .FirstOrDefault(item => string.Equals(item.StepKey, GetOutputValue(routingDecision, "NextStepKey"), StringComparison.OrdinalIgnoreCase));
        }

        nextStep ??= definition.StepDefinitions
            .Where(item => item.StepOrder > currentStep.StepOrder)
            .OrderBy(item => item.StepOrder)
            .FirstOrDefault();

        if (nextStep == null)
        {
            return null;
        }

        var assignmentDecision = EvaluateDecisionTable(definition, nextStep, facts, "assignment");
        var assignment = await ResolveAssignmentAsync(nextStep.AssignmentMode, assignmentDecision, definition.DocumentType, actorUserId, cancellationToken);
        if (!assignment.HasValue)
        {
            return null;
        }

        return new RuleRoutingResult(nextStep, assignment.Value, routingDecision, assignmentDecision);
    }

    private async Task<WorkflowStepDefinition?> ResolveCurrentDefinitionStepAsync(
        WorkflowDefinitionVersion definition,
        string documentType,
        int? documentId,
        int currentStepNumber,
        CancellationToken cancellationToken)
    {
        if (documentId.HasValue && documentId.Value > 0)
        {
            var stepKey = await _context!.WorkflowSteps
                .AsNoTracking()
                .Include(item => item.WorkflowInstance)
                .Where(item => item.WorkflowInstance != null &&
                               item.WorkflowInstance.DocumentType == documentType &&
                               item.WorkflowInstance.DocumentId == documentId.Value &&
                               item.StepNumber == currentStepNumber)
                .OrderByDescending(item => item.Id)
                .Select(item => item.StepKey)
                .FirstOrDefaultAsync(cancellationToken);

            if (!string.IsNullOrWhiteSpace(stepKey))
            {
                return definition.StepDefinitions.FirstOrDefault(item => item.StepKey == stepKey);
            }
        }

        return definition.StepDefinitions.FirstOrDefault(item => item.StepOrder == currentStepNumber);
    }

    private async Task<AssignmentResolution?> ResolveAssignmentAsync(string? assignmentMode, DecisionEvaluationResult? decision, string documentType, string? actorUserId, CancellationToken cancellationToken)
    {
        EnsureContext();
        var mode = assignmentMode?.Trim() ?? WorkflowAssignmentMode.User;
        var approverUserId = GetOutputValue(decision, "AssigneeUserId");
        var approverRoleId = GetOutputValue(decision, "AssigneeRoleId");
        var approverDepartmentId = GetNullableIntOutputValue(decision, "AssigneeDepartmentId");

        if (mode == WorkflowAssignmentMode.User)
        {
            var delegatedTo = await ResolveDelegatedUserAsync(approverUserId, documentType, cancellationToken);
            var userId = delegatedTo ?? approverUserId;
            return string.IsNullOrWhiteSpace(userId) ? null : new AssignmentResolution(userId, null, null);
        }

        if (mode == WorkflowAssignmentMode.Department)
        {
            var managerUserId = await _context!.Departments
                .AsNoTracking()
                .Where(item => item.Id == approverDepartmentId)
                .Select(item => item.ManagerId)
                .FirstOrDefaultAsync(cancellationToken);
            return string.IsNullOrWhiteSpace(managerUserId) ? null : new AssignmentResolution(managerUserId, null, approverDepartmentId);
        }

        if (mode == WorkflowAssignmentMode.Role)
        {
            var firstUserInRole = await GetFirstUserInRoleAsync(approverRoleId, cancellationToken);

            return string.IsNullOrWhiteSpace(firstUserInRole) ? null : new AssignmentResolution(firstUserInRole, approverRoleId, null);
        }

        if (!string.IsNullOrWhiteSpace(actorUserId))
        {
            var managerId = await GetSenderParentManagerAsync(actorUserId, cancellationToken);
            return string.IsNullOrWhiteSpace(managerId) ? null : new AssignmentResolution(managerId, null, null);
        }

        if (!string.IsNullOrWhiteSpace(approverUserId))
        {
            return new AssignmentResolution(approverUserId, approverRoleId, approverDepartmentId);
        }

        return null;
    }

    private async Task<string?> ResolveDelegatedUserAsync(string? originalUserId, string? documentType, CancellationToken cancellationToken)
    {
        EnsureContext();
        if (string.IsNullOrWhiteSpace(originalUserId))
        {
            return null;
        }

        var now = DateTimeOffset.UtcNow;
        return await _context!.WorkflowDelegations
            .AsNoTracking()
            .Where(item =>
                item.IsActive &&
                item.FromUserId == originalUserId &&
                (string.IsNullOrWhiteSpace(item.DocumentType) || item.DocumentType == documentType) &&
                item.StartsAt <= now &&
                item.EndsAt >= now)
            .OrderByDescending(item => item.StartsAt)
            .Select(item => item.ToUserId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<WorkflowInstance?> RequireInstanceAsync(string documentType, int documentId, CancellationToken cancellationToken)
    {
        return await _context!.WorkflowInstances
            .FirstOrDefaultAsync(item => item.DocumentType == documentType && item.DocumentId == documentId, cancellationToken);
    }

    private async Task ReturnToPreviousStepAsync(WorkflowInstance instance, WorkflowStep currentStep, string userId, string? note, CancellationToken cancellationToken)
    {
        var previousStep = instance.Steps
            .Where(item => item.StepNumber < currentStep.StepNumber)
            .OrderByDescending(item => item.StepNumber)
            .FirstOrDefault();

        if (previousStep == null)
        {
            instance.Status = WorkflowStatus.Returned;
            instance.CurrentStatus = WorkflowStatus.Returned;
            instance.CurrentAssigneeUserId = instance.StartedByUserId;
            return;
        }

        instance.CurrentStepNumber = previousStep.StepNumber;
        instance.Status = WorkflowStatus.Returned;
        instance.CurrentStatus = WorkflowStatus.Returned;
        instance.CurrentAssigneeUserId = previousStep.AssignedToUserId;
        instance.CurrentAssigneeRoleId = previousStep.AssignedRoleId;
        instance.CurrentAssigneeDepartmentId = previousStep.AssignedDepartmentId;

        var reopenedStep = new WorkflowStep
        {
            StepNumber = previousStep.StepNumber,
            StepName = previousStep.StepName,
            AssignmentMode = previousStep.AssignmentMode,
            AssignedToUserId = previousStep.AssignedToUserId,
            AssignedRoleId = previousStep.AssignedRoleId,
            AssignedDepartmentId = previousStep.AssignedDepartmentId,
            Status = WorkflowStatus.PendingApproval,
            ReturnedFromStepNumber = currentStep.StepNumber,
            SlaState = WorkflowSlaState.OnTrack
        };
        instance.Steps.Add(reopenedStep);

        if (_slaScheduler != null)
        {
            await _slaScheduler.CancelStepJobsAsync(currentStep.Id, "Step returned to previous stage.", cancellationToken);
            await _slaScheduler.ScheduleStepAsync(instance, reopenedStep, ResolveSlaHours(reopenedStep), cancellationToken);
        }

        await AddActionLogAsync(instance.Id, currentStep.Id, userId, WorkflowDecisionType.Return, note ?? "برگشت به مرحله قبل", null, cancellationToken);
    }

    private async Task ForwardToUserAsync(WorkflowInstance instance, WorkflowStep step, string userId, string? forwardToUserId, string? note, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(forwardToUserId))
        {
            return;
        }

        step.AssignedToUserId = forwardToUserId;
        step.Status = WorkflowStatus.PendingApproval;
        step.CompletedAt = null;
        step.ReadAt = null;
        step.DelegatedFromUserId = userId;
        step.SlaState = WorkflowSlaState.OnTrack;
        instance.Status = WorkflowStatus.PendingApproval;
        instance.CurrentStatus = WorkflowStatus.PendingApproval;
        instance.CurrentAssigneeUserId = forwardToUserId;
        instance.CurrentAssigneeRoleId = null;
        instance.CurrentAssigneeDepartmentId = null;
        instance.SlaState = WorkflowSlaState.OnTrack;
        if (_slaScheduler != null)
        {
            await _slaScheduler.CancelStepJobsAsync(step.Id, "Step forwarded to another user.", cancellationToken);
            await _slaScheduler.ScheduleStepAsync(instance, step, ResolveSlaHours(step), cancellationToken);
        }
        await AddActionLogAsync(instance.Id, step.Id, userId, WorkflowDecisionType.Forward, note ?? "ارجاع شد", JsonSerializer.Serialize(new { forwardToUserId }), cancellationToken);
    }

    private async Task AddActionLogAsync(int instanceId, int? stepId, string actorUserId, string actionType, string? description, string? metadataJson, CancellationToken cancellationToken)
    {
        _context!.WorkflowActionLogs.Add(new WorkflowActionLog
        {
            WorkflowInstanceId = instanceId,
            WorkflowStepId = stepId,
            ActorUserId = actorUserId,
            ActionType = actionType,
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            MetadataJson = metadataJson
        });

        await Task.CompletedTask;
    }

    private async Task MarkIncidentAsync(WorkflowInstance instance, WorkflowStep? step, string? actorUserId, Exception exception, CancellationToken cancellationToken)
    {
        instance.Status = WorkflowStatus.Incident;
        instance.CurrentStatus = WorkflowStatus.Incident;
        instance.LastActionAt = DateTimeOffset.UtcNow;

        _context!.WorkflowIncidents.Add(new WorkflowIncident
        {
            WorkflowInstanceId = instance.Id,
            WorkflowStepId = step?.Id,
            ActorUserId = string.IsNullOrWhiteSpace(actorUserId) ? null : actorUserId,
            IncidentType = exception.GetType().Name,
            ErrorCode = "WorkflowActionFailed",
            ErrorMessage = exception.Message.Length > 2000 ? exception.Message[..2000] : exception.Message,
            ErrorDetails = exception.ToString()
        });

        await RecordTransitionEventAsync(
            instance,
            step,
            actorUserId,
            "workflow.incident",
            instance.Status,
            WorkflowStatus.Incident,
            step?.StepNumber,
            instance.CurrentStepNumber,
            step?.StepKey,
            step?.StepName,
            exception.Message,
            cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogError(exception, "Workflow instance {WorkflowInstanceId} moved to Incident.", instance.Id);
    }

    private async Task CreateDocumentSignatureAsync(
        WorkflowInstance instance,
        WorkflowDecision decision,
        string signerUserId,
        string signatureKey,
        string? signingReason,
        CancellationToken cancellationToken)
    {
        var signer = await _context!.Users
            .AsNoTracking()
            .Where(item => item.Id == signerUserId)
            .Select(item => new { item.FullName, item.UserName })
            .FirstOrDefaultAsync(cancellationToken);

        var businessData = await LoadBusinessDataAsync(instance.DocumentType, instance.DocumentId, cancellationToken);
        var canonicalPayload = BuildCanonicalSignaturePayload(instance, decision, signerUserId, signingReason, businessData);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalPayload)));

        _context.DocumentSignatures.Add(new DocumentSignature
        {
            WorkflowInstanceId = instance.Id,
            WorkflowDecisionId = decision.Id,
            DocumentType = instance.DocumentType,
            DocumentId = instance.DocumentId,
            SignerUserId = signerUserId,
            SignerDisplayName = signer?.FullName ?? signer?.UserName ?? signerUserId,
            SignatureKeyId = signatureKey.Trim(),
            SignatureValue = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes($"{signatureKey.Trim()}:{hash}"))),
            SigningReason = string.IsNullOrWhiteSpace(signingReason) ? null : signingReason.Trim(),
            CanonicalPayload = canonicalPayload,
            PayloadHash = hash,
            SignedAt = DateTimeOffset.UtcNow
        });
    }

    private static string BuildCanonicalSignaturePayload(
        WorkflowInstance instance,
        WorkflowDecision decision,
        string signerUserId,
        string? signingReason,
        object? businessData)
    {
        var payload = new SortedDictionary<string, object?>(StringComparer.Ordinal)
        {
            ["decisionId"] = decision.Id,
            ["decisionType"] = decision.DecisionType,
            ["documentId"] = instance.DocumentId,
            ["documentType"] = instance.DocumentType,
            ["signerUserId"] = signerUserId,
            ["signingReason"] = signingReason,
            ["workflowInstanceId"] = instance.Id,
            ["workflowStepId"] = decision.WorkflowStepId
        };

        if (businessData != null)
        {
            var document = new SortedDictionary<string, object?>(StringComparer.Ordinal);
            foreach (var property in businessData.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance).OrderBy(item => item.Name, StringComparer.Ordinal))
            {
                if (property.CanRead && property.GetIndexParameters().Length == 0 && IsFactSupportedType(property.PropertyType))
                {
                    document[property.Name] = property.GetValue(businessData);
                }
            }

            payload["document"] = document;
        }

        return JsonSerializer.Serialize(payload, CanonicalJsonOptions);
    }

    private async Task RecordTransitionEventAsync(
        WorkflowInstance instance,
        WorkflowStep? step,
        string? actorUserId,
        string eventName,
        string? fromStatus,
        string? toStatus,
        int? fromStepNumber,
        int? toStepNumber,
        string? stationKey,
        string? stationName,
        string? payloadJson,
        CancellationToken cancellationToken)
    {
        EnsureContext();
        var nextSequence = await _context!.WorkflowTransitionEvents
            .Where(item => item.WorkflowInstanceId == instance.Id)
            .Select(item => (long?)item.SequenceNumber)
            .MaxAsync(cancellationToken) ?? 0;

        _context.WorkflowTransitionEvents.Add(new WorkflowTransitionEvent
        {
            WorkflowInstanceId = instance.Id,
            WorkflowStepId = step?.Id,
            SequenceNumber = nextSequence + 1,
            EventName = eventName,
            FromStatus = string.IsNullOrWhiteSpace(fromStatus) ? null : WorkflowStatus.Normalize(fromStatus),
            ToStatus = string.IsNullOrWhiteSpace(toStatus) ? null : WorkflowStatus.Normalize(toStatus),
            FromStepNumber = fromStepNumber,
            ToStepNumber = toStepNumber,
            ActorUserId = string.IsNullOrWhiteSpace(actorUserId) ? null : actorUserId,
            StationKey = stationKey,
            StationName = stationName,
            CorrelationKey = Activity.Current?.Id,
            PayloadJson = payloadJson
        });
    }

    private async Task<object?> LoadBusinessDataAsync(string documentType, int documentId, CancellationToken cancellationToken)
    {
        var entityType = _context!.Model.GetEntityTypes()
            .FirstOrDefault(item => item.ClrType.Name == documentType);
        if (entityType == null)
        {
            return null;
        }

        return await _context.FindAsync(entityType.ClrType, [documentId], cancellationToken);
    }

    private async Task<Dictionary<string, object?>> BuildBusinessFactsAsync(
        string documentType,
        int? documentId,
        string? actorUserId,
        string? currentReceiverId,
        object? businessData,
        CancellationToken cancellationToken)
    {
        var facts = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["DocumentType"] = documentType,
            ["DocumentId"] = documentId,
            ["ActorUserId"] = actorUserId,
            ["CurrentReceiverId"] = currentReceiverId
        };

        if (!string.IsNullOrWhiteSpace(actorUserId))
        {
            var actor = await _context!.Users
                .AsNoTracking()
                .Where(item => item.Id == actorUserId)
                .Select(item => new
                {
                    item.Id,
                    item.DepartmentId,
                    item.ParentManagerUserId,
                    item.IsManager
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (actor != null)
            {
                facts["ActorDepartmentId"] = actor.DepartmentId;
                facts["ActorManagerUserId"] = actor.ParentManagerUserId;
                facts["ActorIsManager"] = actor.IsManager;
            }
        }

        if (businessData != null)
        {
            foreach (var property in businessData.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!property.CanRead || property.GetIndexParameters().Length > 0)
                {
                    continue;
                }

                if (!IsFactSupportedType(property.PropertyType))
                {
                    continue;
                }

                facts[property.Name] = property.GetValue(businessData);
            }
        }

        return facts;
    }

    private static bool IsFactSupportedType(Type type)
    {
        var candidate = Nullable.GetUnderlyingType(type) ?? type;
        return candidate.IsPrimitive ||
               candidate.IsEnum ||
               candidate == typeof(string) ||
               candidate == typeof(decimal) ||
               candidate == typeof(DateTime) ||
               candidate == typeof(DateTimeOffset) ||
               candidate == typeof(Guid);
    }

    private DecisionEvaluationResult EvaluateDecisionTable(
        WorkflowDefinitionVersion definition,
        WorkflowStepDefinition stepDefinition,
        IReadOnlyDictionary<string, object?> facts,
        string purpose)
    {
        var table = BuildDecisionTable(definition, stepDefinition, purpose);
        var result = _decisionEngine.Evaluate(table, facts);
        _logger.LogInformation(
            "Workflow decision evaluated for {DocumentType}/{StepKey}/{Purpose}. Version={VersionTag} MatchedRule={RuleId}",
            definition.DocumentType,
            stepDefinition.StepKey,
            purpose,
            result.VersionTag,
            result.MatchedRuleId);
        return result;
    }

    internal DecisionTableDefinition BuildDecisionTable(WorkflowDefinitionVersion definition, WorkflowStepDefinition stepDefinition, string purpose)
    {
        return new DecisionTableDefinition
        {
            TableKey = $"{definition.DocumentType}:{stepDefinition.StepKey}:{purpose}",
            VersionTag = $"{definition.Id}:v{definition.Version}",
            Name = $"{definition.DocumentType} {stepDefinition.StepKey} {purpose}",
            Rules = stepDefinition.Rules
                .OrderBy(item => item.Id)
                .Select((item, index) => new DecisionRuleDefinition
                {
                    RuleId = $"rule-{item.Id}",
                    SortOrder = index + 1,
                    FieldName = item.FieldName,
                    Operator = item.Operator,
                    Value = item.Value,
                    Outputs = new Dictionary<string, object?>
                    {
                        ["NextStepKey"] = item.NextStepKey,
                        ["AssigneeUserId"] = item.AssigneeUserId,
                        ["AssigneeRoleId"] = item.AssigneeRoleId,
                        ["AssigneeDepartmentId"] = item.AssigneeDepartmentId
                    }
                })
                .ToList()
        };
    }

    public DecisionRegressionReport RunDecisionRegression(
        WorkflowDefinitionVersion definition,
        WorkflowStepDefinition stepDefinition,
        string purpose,
        IEnumerable<DecisionRegressionCase> cases)
    {
        return _decisionEngine.RunRegression(BuildDecisionTable(definition, stepDefinition, purpose), cases);
    }

    private static string? GetOutputValue(DecisionEvaluationResult? result, string key)
    {
        return result?.Outputs.TryGetValue(key, out var value) == true
            ? Convert.ToString(value, CultureInfo.InvariantCulture)
            : null;
    }

    private static int? GetNullableIntOutputValue(DecisionEvaluationResult? result, string key)
    {
        if (result?.Outputs.TryGetValue(key, out var value) != true || value == null)
        {
            return null;
        }

        if (value is int intValue)
        {
            return intValue;
        }

        return int.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static int ResolveSlaHours(WorkflowStepDefinition? stepDefinition)
    {
        return stepDefinition?.SlaHours > 0 ? stepDefinition.SlaHours : 24;
    }

    private static int ResolveSlaHours(WorkflowStep step)
    {
        if (step.DueAt.HasValue)
        {
            var inferred = (int)Math.Ceiling((step.DueAt.Value - step.CreatedAt).TotalHours);
            if (inferred > 0)
            {
                return inferred;
            }
        }

        return 24;
    }

    private static bool IsAssignedToUser(WorkflowStep step, string userId)
    {
        return !string.IsNullOrWhiteSpace(userId) &&
               (string.Equals(step.AssignedToUserId, userId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(step.DelegatedFromUserId, userId, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<bool> IsAssignedToUserAsync(WorkflowStep step, string userId, CancellationToken cancellationToken)
    {
        if (IsAssignedToUser(step, userId))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(userId) || _context == null)
        {
            return false;
        }

        if (step.AssignedDepartmentId.HasValue)
        {
            var userDepartmentId = await _context.Users
                .AsNoTracking()
                .Where(item => item.Id == userId)
                .Select(item => item.DepartmentId)
                .FirstOrDefaultAsync(cancellationToken);

            if (userDepartmentId.HasValue && userDepartmentId.Value == step.AssignedDepartmentId.Value)
            {
                return true;
            }
        }

        if (!string.IsNullOrWhiteSpace(step.AssignedRoleId))
        {
            return await IsUserInRoleAsync(userId, step.AssignedRoleId, cancellationToken);
        }

        return false;
    }

    private void EnsureContext()
    {
        if (_context == null)
        {
            throw new InvalidOperationException("WorkflowService requires a database context for this operation.");
        }
    }

    private async Task<string?> GetFirstUserInRoleAsync(string? roleId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(roleId))
        {
            return null;
        }

        var userRoles = QueryUserRoles();
        if (userRoles == null)
        {
            return null;
        }

        return await (from userRole in userRoles
                      where userRole.RoleId == roleId
                      join user in _context!.Users on userRole.UserId equals user.Id
                      orderby user.FullName
                      select user.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<bool> IsUserInRoleAsync(string userId, string roleId, CancellationToken cancellationToken)
    {
        var userRoles = QueryUserRoles();
        return userRoles != null &&
               await userRoles.AnyAsync(item => item.UserId == userId && item.RoleId == roleId, cancellationToken);
    }

    private IQueryable<IdentityUserRole<string>>? QueryUserRoles()
    {
        return _identityContext?.UserRoles.AsNoTracking()
            ?? (_context as DbContext)?.Set<IdentityUserRole<string>>().AsNoTracking();
    }

    private void EnqueueWorkflowStatusEvent(
        WorkflowInstance instance,
        WorkflowStep? step,
        string? actorUserId,
        string decisionType,
        string previousStatus,
        string currentStatus,
        string? comment)
    {
        if (_context == null || _outboxService == null || string.IsNullOrWhiteSpace(actorUserId))
        {
            return;
        }

        _outboxService.EnqueueWorkflowStatusChanged(_context, new WorkflowStatusChangedIntegrationEvent
        {
            DocumentType = instance.DocumentType,
            DocumentId = instance.DocumentId,
            WorkflowInstanceId = instance.Id,
            WorkflowStepId = step?.Id,
            StepNumber = step?.StepNumber ?? instance.CurrentStepNumber,
            DecisionType = decisionType,
            PreviousStatus = WorkflowStatus.Normalize(previousStatus),
            CurrentStatus = WorkflowStatus.Normalize(currentStatus),
            CurrentWorkflowStatus = WorkflowStatus.Normalize(instance.Status),
            CurrentAssigneeUserId = instance.CurrentAssigneeUserId,
            CurrentAssigneeRoleId = instance.CurrentAssigneeRoleId,
            CurrentAssigneeDepartmentId = instance.CurrentAssigneeDepartmentId,
            ActorUserId = actorUserId,
            Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim(),
            CorrelationId = Activity.Current?.Id
        });
    }

    private static DecisionExplanationEnvelope CreateDecisionEnvelope(
        string decisionContext,
        WorkflowDefinitionVersion? definition,
        DecisionEvaluationResult? routingDecision,
        DecisionEvaluationResult? assignmentDecision)
    {
        return new DecisionExplanationEnvelope
        {
            DecisionContext = decisionContext,
            DefinitionVersionTag = definition == null ? null : $"{definition.Id}:v{definition.Version}",
            RoutingDecision = routingDecision,
            AssignmentDecision = assignmentDecision
        };
    }

    private readonly record struct AssignmentResolution(string? UserId, string? RoleId, int? DepartmentId);

    private readonly record struct RuleRoutingResult(
        WorkflowStepDefinition Step,
        AssignmentResolution Assignment,
        DecisionEvaluationResult? RoutingDecision,
        DecisionEvaluationResult? AssignmentDecision);
}

public sealed class WorkflowRuleEvaluationResult
{
    public int DefinitionVersionId { get; init; }
    public int DefinitionVersion { get; init; }
    public string? CurrentStepKey { get; init; }
    public string NextStepKey { get; init; } = string.Empty;
    public int NextStepOrder { get; init; }
    public string? AssigneeUserId { get; init; }
    public string? AssigneeRoleId { get; init; }
    public int? AssigneeDepartmentId { get; init; }
    public IReadOnlyDictionary<string, object?> Facts { get; init; } = new Dictionary<string, object?>();
    public DecisionExplanationEnvelope? Explanation { get; init; }
}

