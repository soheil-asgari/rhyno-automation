using OfficeAutomation.Services.Licensing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeAutomation.DTOs.Api.v1;
using OfficeAutomation.Models;
using OfficeAutomation.Modules.Office.Infrastructure.Persistence;
using OfficeAutomation.Services;
using OfficeAutomation.Services.Security;

namespace OfficeAutomation.Controllers.Api.v1;

/// <summary>
/// کنترلر API منابع انسانی - مدیریت پرسنل و درخواست‌های مرخصی
/// </summary>
[ApiController]
[Route("api/v1/hr")]
[Authorize(AuthenticationSchemes = ApiKeyAuthenticationExtensions.AuthenticationScheme)]
[Produces("application/json")]
[RequiresModule(LicensableModules.HumanCapital)]
public sealed class HrApiController : ApiBaseController
{
    private readonly OfficeDbContext _context;
    private readonly WorkflowService _workflowService;
    private readonly LeaveWorkflowService _leaveWorkflowService;

    /// <summary>
    /// سازنده کنترلر API منابع انسانی
    /// </summary>
    public HrApiController(
        OfficeDbContext context,
        WorkflowService workflowService,
        LeaveWorkflowService leaveWorkflowService)
    {
        _context = context;
        _workflowService = workflowService;
        _leaveWorkflowService = leaveWorkflowService;
    }

    /// <summary>
    /// دریافت لیست پرسنل با صفحه‌بندی و فیلترهای جستجو
    /// </summary>
    /// <param name="filter">فیلترهای جستجو شامل وضعیت، نوع استخدام، دپارتمان و جستجوی متنی</param>
    /// <param name="cancellationToken">توکن لغو درخواست</param>
    /// <returns>لیست صفحه‌بندی شده پرسنل</returns>
    [HttpGet("employees")]
    [ProducesResponseType(typeof(ApiPagedResponse<EmployeeSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetEmployees(
        [FromQuery] EmployeeFilterRequest filter,
        CancellationToken cancellationToken)
    {
        var query = _context.HumanCapitalEmployees
            .AsNoTracking()
            .Include(e => e.Department)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim();
            query = query.Where(e =>
                e.FullName.Contains(search) ||
                e.PersonnelCode.Contains(search) ||
                e.NationalCode.Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            query = query.Where(e => e.CurrentStatus == filter.Status.Trim());
        }

        if (!string.IsNullOrWhiteSpace(filter.EmploymentType))
        {
            query = query.Where(e => e.EmploymentType == filter.EmploymentType.Trim());
        }

        if (filter.DepartmentId.HasValue)
        {
            query = query.Where(e => e.DepartmentId == filter.DepartmentId.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(e => e.FullName)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(e => EmployeeSummaryDto.From(e))
            .ToListAsync(cancellationToken);

        return ApiPaged(items, totalCount, filter.Page, filter.PageSize);
    }

    /// <summary>
    /// دریافت لیست درخواست‌های مرخصی
    /// </summary>
    /// <param name="status">فیلتر وضعیت (اختیاری: در انتظار/تایید شده/رد شده)</param>
    /// <param name="userId">فیلتر بر اساس کاربر (اختیاری)</param>
    /// <param name="page">شماره صفحه (پیش‌فرض: ۱)</param>
    /// <param name="pageSize">تعداد در هر صفحه (پیش‌فرض: ۲۰)</param>
    /// <param name="cancellationToken">توکن لغو درخواست</param>
    /// <returns>لیست صفحه‌بندی شده درخواست‌های مرخصی</returns>
    [HttpGet("leaves")]
    [ProducesResponseType(typeof(ApiPagedResponse<LeaveSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetLeaves(
        [FromQuery] string? status,
        [FromQuery] string? userId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var pageSizeClamped = Math.Clamp(pageSize, 1, 100);

        var query = _context.Leaves.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(l => l.Status == status.Trim());
        }

        if (!string.IsNullOrWhiteSpace(userId))
        {
            query = query.Where(l => l.UserId == userId.Trim());
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(l => l.EndDate)
            .Skip((page - 1) * pageSizeClamped)
            .Take(pageSizeClamped)
            .Select(l => LeaveSummaryDto.From(l))
            .ToListAsync(cancellationToken);

        return ApiPaged(items, totalCount, page, pageSizeClamped);
    }

    /// <summary>
    /// ثبت درخواست مرخصی جدید
    /// </summary>
    /// <param name="request">اطلاعات درخواست مرخصی شامل تاریخ شروع، پایان و دلیل</param>
    /// <param name="cancellationToken">توکن لغو درخواست</param>
    /// <returns>درخواست مرخصی ثبت شده</returns>
    [HttpPost("leaves")]
    [ProducesResponseType(typeof(ApiResponse<LeaveSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateLeave(
        [FromBody] CreateLeaveRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();
            return ApiError("اطلاعات ورودی نامعتبر است.", StatusCodes.Status400BadRequest, errors);
        }

        if (request.EndDate < request.StartDate)
        {
            return ApiError("تاریخ پایان نمی‌تواند قبل از تاریخ شروع باشد.", StatusCodes.Status400BadRequest);
        }

        if ((request.EndDate - request.StartDate).TotalDays > 60)
        {
            return ApiError("مدت مرخصی نمی‌تواند بیش از ۶۰ روز باشد.", StatusCodes.Status400BadRequest);
        }

        var leave = new Leave
        {
            UserId = CurrentUserId ?? "api-client",
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Reason = request.Reason.Trim(),
            Status = WorkflowStatus.PendingApproval
        };

        _context.Leaves.Add(leave);
        await _context.SaveChangesAsync(cancellationToken);

        await _workflowService.StartRoutingAsync(
            nameof(Leave),
            CurrentUserId ?? string.Empty,
            CurrentUserId ?? string.Empty,
            leave.Id);

        return ApiOk(LeaveSummaryDto.From(leave), "درخواست مرخصی با موفقیت ثبت شد.");
    }
}