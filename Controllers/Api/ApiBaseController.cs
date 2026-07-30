using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace OfficeAutomation.Controllers.Api;

/// <summary>
/// کنترلر پایه API - احراز هویت مبتنی بر API Key و JWT
/// </summary>
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[Produces("application/json")]
[Authorize(AuthenticationSchemes = "ApiKey,Bearer")]
[EnableRateLimiting("api")]
public abstract class ApiBaseController : ControllerBase
{
    /// <summary>
    /// شناسه tenant از context جاری
    /// </summary>
    protected string CurrentTenantId =>
        HttpContext.Items["TenantId"] as string ?? "default";

    /// <summary>
    /// شناسه کاربر جاری
    /// </summary>
    protected string? CurrentUserId =>
        User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

    /// <summary>
    /// بازگشت پاسخ موفق استاندارد
    /// </summary>
    protected IActionResult ApiOk<T>(T data, string message = "عملیات با موفقیت انجام شد.")
    {
        return Ok(new ApiResponse<T>
        {
            Success = true,
            Message = message,
            Data = data,
            Timestamp = DateTimeOffset.UtcNow
        });
    }

    /// <summary>
    /// بازگشت پاسخ خطا استاندارد
    /// </summary>
    protected IActionResult ApiError(string message, int statusCode = 400, List<string>? errors = null)
    {
        return StatusCode(statusCode, new ApiResponse<object?>
        {
            Success = false,
            Message = message,
            Data = null,
            Errors = errors ?? new List<string>(),
            Timestamp = DateTimeOffset.UtcNow
        });
    }

    /// <summary>
    /// بازگشت پاسخ صفحه‌بندی شده
    /// </summary>
    protected IActionResult ApiPaged<T>(IEnumerable<T> data, int totalCount, int page, int pageSize)
    {
        return Ok(new ApiPagedResponse<T>
        {
            Success = true,
            Message = "داده‌ها با موفقیت دریافت شدند.",
            Data = data,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
            Timestamp = DateTimeOffset.UtcNow
        });
    }
}

/// <summary>
/// مدل پاسخ استاندارد API
/// </summary>
public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public T? Data { get; set; }
    public List<string>? Errors { get; set; }
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// مدل پاسخ صفحه‌بندی شده API
/// </summary>
public class ApiPagedResponse<T> : ApiResponse<IEnumerable<T>>
{
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}

/// <summary>
/// مدل درخواست صفحه‌بندی
/// </summary>
public class PagedRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Search { get; set; }
    public string? SortBy { get; set; }
    public bool SortDescending { get; set; }
}

/// <summary>
/// مدل فیلتر عمومی
/// </summary>
public class FilterRequest : PagedRequest
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? Status { get; set; }
}