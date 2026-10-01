using OfficeAutomation.Services.Licensing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeAutomation.DTOs.Api.v1;
using OfficeAutomation.Models;
using OfficeAutomation.Modules.Workflow.Infrastructure.Persistence;
using OfficeAutomation.Services;
using OfficeAutomation.Services.Security;

namespace OfficeAutomation.Controllers.Api.v1;

/// <summary>
/// کنترلر API مدیریت گردش کار و تسک‌ها - کارتابل، تایید و رد تسک‌ها
/// </summary>
[ApiController]
[Route("api/v1/workflow")]
[Authorize(AuthenticationSchemes = ApiKeyAuthenticationExtensions.AuthenticationScheme)]
[Produces("application/json")]
[RequiresModule(LicensableModules.Workflow)]
public sealed class WorkflowApiController : ApiBaseController
{
    private readonly IWorkflowDbContext _context;
    private readonly WorkflowService _workflowService;

    /// <summary>
    /// سازنده کنترلر API گردش کار
    /// </summary>
    public WorkflowApiController(
        IWorkflowDbContext context,
        WorkflowService workflowService)
    {
        _context = context;
        _workflowService = workflowService;
    }

    /// <summary>
    /// دریافت لیست تسک‌های در انتظار تایید (کارتابل)
    /// </summary>
    /// <param name="cancellationToken">توکن لغو درخواست</param>
    /// <returns>لیست تسک‌های باز و در انتظار اقدام</returns>
    [HttpGet("tasks/pending")]
    [ProducesResponseType(typeof(ApiResponse<List<PendingTaskDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetPendingTasks(CancellationToken cancellationToken)
    {
        var pendingInstances = await _context.WorkflowInstances
            .AsNoTracking()
            .Include(w => w.StartedByUser)
            .Where(w =>
                w.Status == WorkflowStatus.PendingApproval ||
                w.Status == WorkflowStatus.Sent ||
                w.Status == WorkflowStatus.Draft)
            .OrderByDescending(w => w.Priority)
            .ThenByDescending(w => w.StartedAt)
            .Take(200)
            .Select(w => PendingTaskDto.From(w))
            .ToListAsync(cancellationToken);

        return ApiOk(pendingInstances, "لیست تسک‌های در انتظار با موفقیت دریافت شد.");
    }

    /// <summary>
    /// تایید یک تسک در گردش کار
    /// </summary>
    /// <param name="id">شناسه یکتای نمونه گردش کار</param>
    /// <param name="request">اطلاعات تایید شامل دلیل و توضیحات (اختیاری)</param>
    /// <param name="cancellationToken">توکن لغو درخواست</param>
    /// <returns>نتیجه تایید تسک</returns>
    [HttpPost("tasks/{id:int}/approve")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ApproveTask(
        [FromRoute] int id,
        [FromBody] TaskDecisionRequest? request,
        CancellationToken cancellationToken)
    {
        var instance = await _context.WorkflowInstances
            .FirstOrDefaultAsync(w => w.Id == id, cancellationToken);

        if (instance == null)
        {
            return ApiError("نمونه گردش کار مورد نظر یافت نشد.", StatusCodes.Status404NotFound);
        }

        if (!_workflowService.IsOpen(instance.Status))
        {
            return ApiError("این تسک قبلاً بسته شده است و امکان تایید مجدد وجود ندارد.",
                StatusCodes.Status400BadRequest);
        }

        instance.Status = WorkflowStatus.Approved;
        instance.CurrentStatus = WorkflowStatus.Approved;
        instance.CompletedAt = DateTimeOffset.UtcNow;
        instance.LastActionAt = DateTimeOffset.UtcNow;

        var userId = CurrentUserId ?? "api-client";

        await _workflowService.RecordDecisionAsync(
            instance.DocumentType,
            instance.DocumentId,
            instance.CurrentStepNumber,
            userId,
            WorkflowStatus.Approved,
            request?.Reason,
            cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        return ApiOk(
            new { id = instance.Id, status = instance.Status, message = "تسک با موفقیت تایید شد." },
            "تسک تایید شد.");
    }

    /// <summary>
    /// رد یک تسک در گردش کار با ذکر دلیل
    /// </summary>
    /// <param name="id">شناسه یکتای نمونه گردش کار</param>
    /// <param name="request">اطلاعات رد شامل دلیل الزامی و توضیحات تکمیلی</param>
    /// <param name="cancellationToken">توکن لغو درخواست</param>
    /// <returns>نتیجه رد تسک</returns>
    [HttpPost("tasks/{id:int}/reject")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> RejectTask(
        [FromRoute] int id,
        [FromBody] TaskDecisionRequest request,
        CancellationToken cancellationToken)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Reason))
        {
            return ApiError("ذکر دلیل برای رد تسک الزامی است.", StatusCodes.Status400BadRequest);
        }

        var instance = await _context.WorkflowInstances
            .FirstOrDefaultAsync(w => w.Id == id, cancellationToken);

        if (instance == null)
        {
            return ApiError("نمونه گردش کار مورد نظر یافت نشد.", StatusCodes.Status404NotFound);
        }

        if (!_workflowService.IsOpen(instance.Status))
        {
            return ApiError("این تسک قبلاً بسته شده است و امکان رد مجدد وجود ندارد.",
                StatusCodes.Status400BadRequest);
        }

        instance.Status = WorkflowStatus.Rejected;
        instance.CurrentStatus = WorkflowStatus.Rejected;
        instance.CompletedAt = DateTimeOffset.UtcNow;
        instance.LastActionAt = DateTimeOffset.UtcNow;

        var userId = CurrentUserId ?? "api-client";

        await _workflowService.RecordDecisionAsync(
            instance.DocumentType,
            instance.DocumentId,
            instance.CurrentStepNumber,
            userId,
            WorkflowStatus.Rejected,
            request.Reason,
            cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        return ApiOk(
            new { id = instance.Id, status = instance.Status, reason = request.Reason, message = "تسک با موفقیت رد شد." },
            "تسک رد شد.");
    }
}