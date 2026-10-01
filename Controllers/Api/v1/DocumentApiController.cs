using OfficeAutomation.Services.Licensing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeAutomation.DTOs.Api.v1;
using OfficeAutomation.Models;
using OfficeAutomation.Modules.Workflow.Infrastructure.Persistence;
using OfficeAutomation.Services.Security;
using OfficeAutomation.Services.Tenancy;

namespace OfficeAutomation.Controllers.Api.v1;

/// <summary>
/// کنترلر API مدیریت اسناد - جستجو، آپلود و دانلود اسناد بایگانی
/// </summary>
[ApiController]
[Route("api/v1/documents")]
[Authorize(AuthenticationSchemes = ApiKeyAuthenticationExtensions.AuthenticationScheme)]
[Produces("application/json")]
[RequiresModule(LicensableModules.OfficeAutomation)]
public sealed class DocumentApiController : ApiBaseController
{
    private readonly IWorkflowDbContext _context;
    private readonly IWebHostEnvironment _environment;
    private readonly ITenantPathResolver _tenantPathResolver;
    private readonly IPermissionAccessService _permissionAccessService;

    /// <summary>
    /// سازنده کنترلر API اسناد
    /// </summary>
    public DocumentApiController(
        IWorkflowDbContext context,
        IWebHostEnvironment environment,
        ITenantPathResolver tenantPathResolver,
        IPermissionAccessService permissionAccessService)
    {
        _context = context;
        _environment = environment;
        _tenantPathResolver = tenantPathResolver;
        _permissionAccessService = permissionAccessService;
    }

    /// <summary>
    /// جستجو و دریافت لیست اسناد بایگانی با صفحه‌بندی
    /// </summary>
    /// <param name="filter">فیلترهای جستجو شامل دسته‌بندی و جستجوی متنی</param>
    /// <param name="cancellationToken">توکن لغو درخواست</param>
    /// <returns>لیست صفحه‌بندی شده اسناد بایگانی</returns>
    [HttpGet]
    [ProducesResponseType(typeof(ApiPagedResponse<DocumentSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetDocuments(
        [FromQuery] DocumentFilterRequest filter,
        CancellationToken cancellationToken)
    {
        var query = _context.DocumentArchiveItems
            .AsNoTracking()
            .Include(d => d.CreatedByUser)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.Category))
        {
            query = query.Where(d => d.Category == filter.Category.Trim());
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim();
            query = query.Where(d =>
                d.Title.Contains(search) ||
                d.FileName.Contains(search));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(d => d.CreatedAt)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(d => DocumentSummaryDto.From(d))
            .ToListAsync(cancellationToken);

        return ApiPaged(items, totalCount, filter.Page, filter.PageSize);
    }

    /// <summary>
    /// آپلود سند جدید به بایگانی
    /// </summary>
    /// <param name="request">اطلاعات سند شامل فایل، عنوان، دسته‌بندی و سطح دسترسی</param>
    /// <param name="cancellationToken">توکن لغو درخواست</param>
    /// <returns>نتیجه آپلود سند</returns>
    [HttpPost("upload")]
    [ProducesResponseType(typeof(ApiResponse<DocumentSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UploadDocument(
        [FromForm] DocumentUploadRequest request,
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

        if (request.File == null || request.File.Length == 0)
        {
            return ApiError("فایل انتخابی معتبر نیست.", StatusCodes.Status400BadRequest);
        }

        try
        {
            ValidateUploadedFile(request.File);
        }
        catch (InvalidOperationException ex)
        {
            return ApiError(ex.Message, StatusCodes.Status400BadRequest);
        }

        var userId = CurrentUserId ?? "api-client";
        var safeTitle = request.Title.Trim();
        var extension = Path.GetExtension(request.File.FileName);
        var storedFileName = $"{Guid.NewGuid():N}{extension}";
        var uploadRoot = _tenantPathResolver.GetArchiveRoot(
            Path.Combine(_environment.ContentRootPath, "App_Data"));
        Directory.CreateDirectory(uploadRoot);
        var fullPath = Path.Combine(uploadRoot, storedFileName);

        await using (var stream = new FileStream(fullPath, FileMode.Create))
        {
            await request.File.CopyToAsync(stream, cancellationToken);
        }

        var contentType = string.IsNullOrWhiteSpace(request.File.ContentType)
            ? "application/octet-stream"
            : request.File.ContentType;

        var relativePath = _tenantPathResolver.GetTenantRelativePath(
            "uploads", "archive", storedFileName);

        var isPreviewable = contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ||
                            contentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase) ||
                            contentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase);

        var entity = new DocumentArchiveItem
        {
            Title = safeTitle,
            Category = request.Category,
            AccessLevel = request.AccessLevel,
            RelatedModule = request.RelatedModule?.Trim(),
            RelatedEntityType = request.RelatedEntityType?.Trim(),
            RelatedEntityId = request.RelatedEntityId,
            FileName = request.File.FileName,
            StoredFileName = storedFileName,
            RelativePath = relativePath,
            ContentType = contentType,
            FileSize = request.File.Length,
            IsPreviewable = isPreviewable,
            CreatedByUserId = userId
        };

        _context.DocumentArchiveItems.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);

        var resultDto = DocumentSummaryDto.From(entity);
        return ApiOk(resultDto, "سند با موفقیت در بایگانی ذخیره شد.");
    }

    /// <summary>
    /// دانلود فایل سند بایگانی شده
    /// </summary>
    /// <param name="id">شناسه یکتای سند</param>
    /// <param name="cancellationToken">توکن لغو درخواست</param>
    /// <returns>فایل سند برای دانلود</returns>
    [HttpGet("{id:int}/download")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DownloadDocument(
        [FromRoute] int id,
        CancellationToken cancellationToken)
    {
        var item = await _context.DocumentArchiveItems
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

        if (item == null)
        {
            return ApiError("سند مورد نظر یافت نشد.", StatusCodes.Status404NotFound);
        }

        var physicalPath = _tenantPathResolver.MapRelativeToPhysical(
            Path.Combine(_environment.ContentRootPath, "App_Data"),
            item.RelativePath);

        if (!System.IO.File.Exists(physicalPath))
        {
            physicalPath = _tenantPathResolver.MapRelativeToPhysical(
                _environment.WebRootPath,
                item.RelativePath);

            if (!System.IO.File.Exists(physicalPath))
            {
                return ApiError("فایل فیزیکی سند یافت نشد.", StatusCodes.Status404NotFound);
            }
        }

        var bytes = await System.IO.File.ReadAllBytesAsync(physicalPath, cancellationToken);
        return File(bytes, item.ContentType ?? "application/octet-stream", item.FileName);
    }

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".png", ".jpg", ".jpeg", ".doc", ".docx", ".xls", ".xlsx", ".txt", ".csv", ".zip", ".rar"
    };

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf",
        "image/png",
        "image/jpeg",
        "application/msword",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/vnd.ms-excel",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "text/plain",
        "text/csv",
        "application/zip",
        "application/x-zip-compressed",
        "application/x-rar-compressed"
    };

    private const long MaxFileBytes = 20 * 1024 * 1024;

    private static void ValidateUploadedFile(IFormFile file)
    {
        if (file.Length <= 0)
        {
            throw new InvalidOperationException("فایل خالی است.");
        }

        if (file.Length > MaxFileBytes)
        {
            throw new InvalidOperationException("حجم فایل بیشتر از حد مجاز (۲۰ مگابایت) است.");
        }

        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
        {
            throw new InvalidOperationException("پسوند فایل مجاز نیست.");
        }

        if (!string.IsNullOrWhiteSpace(file.ContentType) &&
            !AllowedContentTypes.Contains(file.ContentType))
        {
            throw new InvalidOperationException("نوع محتوای فایل مجاز نیست.");
        }
    }
}