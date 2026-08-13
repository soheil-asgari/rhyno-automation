using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Models;
using OfficeAutomation.Modules.Workflow.Infrastructure.Persistence;
using OfficeAutomation.Services;
using OfficeAutomation.Services.Security;
using System.Data;
using System.Security.Claims;

namespace OfficeAutomation.Controllers
{
    [Authorize]
    [PermissionAuthorize("Archive.View")]
    public class DocumentArchiveController : Controller
    {
        private readonly IWorkflowDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly IPermissionAccessService _permissionAccessService;
        private readonly Services.Tenancy.ITenantPathResolver _tenantPathResolver;
        private readonly IDocumentVersioningService _versioningService;

        public DocumentArchiveController(
            IWorkflowDbContext context,
            IWebHostEnvironment environment,
            IPermissionAccessService permissionAccessService,
            Services.Tenancy.ITenantPathResolver tenantPathResolver,
            IDocumentVersioningService versioningService)
        {
            _context = context;
            _environment = environment;
            _permissionAccessService = permissionAccessService;
            _tenantPathResolver = tenantPathResolver;
            _versioningService = versioningService;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? searchTerm, string? category, string? accessLevel, CancellationToken cancellationToken)
        {
            var query = _context.DocumentArchiveItems
                .AsNoTracking()
                .Include(item => item.CreatedByUser)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim();
                query = query.Where(item => item.Title.Contains(term) || item.FileName.Contains(term));
            }

            if (!string.IsNullOrWhiteSpace(category))
            {
                query = query.Where(item => item.Category == category);
            }

            if (!string.IsNullOrWhiteSpace(accessLevel))
            {
                query = query.Where(item => item.AccessLevel == accessLevel);
            }

            var rows = await query
                .OrderByDescending(item => item.CreatedAt)
                .Take(500)
                .ToListAsync(cancellationToken);

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            var visibleRows = new List<DocumentArchiveItem>();
            foreach (var row in rows)
            {
                if (await CanReadItemAsync(userId, row, cancellationToken))
                {
                    visibleRows.Add(row);
                }
            }

            var model = new DocumentArchiveIndexVM
            {
                SearchTerm = searchTerm,
                Category = category,
                AccessLevel = accessLevel,
                CanUpload = await _permissionAccessService.UserHasPermissionAsync(userId, "Archive.Create", cancellationToken),
                Items = visibleRows.Select(item => new DocumentArchiveItemVM
                {
                    Id = item.Id,
                    Title = item.Title,
                    Category = item.Category,
                    AccessLevel = item.AccessLevel,
                    FileName = item.FileName,
                    RelativePath = item.RelativePath,
                    ContentType = item.ContentType,
                    FileSize = item.FileSize,
                    IsPreviewable = item.IsPreviewable,
                    CreatedAt = item.CreatedAt,
                    CreatorName = item.CreatedByUser?.FullName ?? item.CreatedByUser?.UserName ?? "کاربر"
                }).ToList()
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Archive.Create")]
        public async Task<IActionResult> Upload(DocumentArchiveUploadVM model, CancellationToken cancellationToken)
        {
            if (model.File == null || model.File.Length == 0)
            {
                TempData["ArchiveMessage"] = "فایل انتخابی معتبر نیست.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                ValidateUploadedFile(model.File);
            }
            catch (InvalidOperationException ex)
            {
                TempData["ArchiveMessage"] = ex.Message;
                return RedirectToAction(nameof(Index));
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            var safeTitle = model.Title.Trim();
            var extension = Path.GetExtension(model.File.FileName);
            var storedFileName = $"{Guid.NewGuid():N}{extension}";
            var uploadRoot = _tenantPathResolver.GetArchiveRoot(Path.Combine(_environment.ContentRootPath, "App_Data"));
            Directory.CreateDirectory(uploadRoot);
            var fullPath = Path.Combine(uploadRoot, storedFileName);

            await using (var stream = new FileStream(fullPath, FileMode.Create))
            {
                await model.File.CopyToAsync(stream, cancellationToken);
            }

            var contentType = string.IsNullOrWhiteSpace(model.File.ContentType)
                ? "application/octet-stream"
                : model.File.ContentType;

            var relativePath = _tenantPathResolver.GetTenantRelativePath("uploads", "archive", storedFileName);
            _context.DocumentArchiveItems.Add(new DocumentArchiveItem
            {
                Title = safeTitle,
                Category = model.Category,
                AccessLevel = model.AccessLevel,
                RelatedModule = model.RelatedModule?.Trim(),
                RelatedEntityType = model.RelatedEntityType?.Trim(),
                RelatedEntityId = model.RelatedEntityId,
                FileName = model.File.FileName,
                StoredFileName = storedFileName,
                RelativePath = relativePath,
                ContentType = contentType,
                FileSize = model.File.Length,
                IsPreviewable = IsPreviewableContentType(contentType),
                CreatedByUserId = userId
            });

            await _context.SaveChangesAsync(cancellationToken);
            TempData["ArchiveMessage"] = "فایل با موفقیت بایگانی شد.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Preview(int id, CancellationToken cancellationToken)
        {
            var item = await _context.DocumentArchiveItems
                .AsNoTracking()
                .FirstOrDefaultAsync(row => row.Id == id, cancellationToken);
            if (item == null)
            {
                return NotFound();
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            if (!await CanReadItemAsync(userId, item, cancellationToken))
            {
                return Forbid();
            }

            if (!item.IsPreviewable)
            {
                return BadRequest("این فایل قابل پیش‌نمایش نیست.");
            }

            var physicalPath = _tenantPathResolver.MapRelativeToPhysical(Path.Combine(_environment.ContentRootPath, "App_Data"), item.RelativePath);
            if (!System.IO.File.Exists(physicalPath))
            {
                physicalPath = _tenantPathResolver.MapRelativeToPhysical(_environment.WebRootPath, item.RelativePath);
                if (!System.IO.File.Exists(physicalPath))
                {
                    return NotFound();
                }
            }

            var bytes = await System.IO.File.ReadAllBytesAsync(physicalPath, cancellationToken);
            return File(bytes, item.ContentType ?? "application/octet-stream");
        }

        [HttpGet]
        public async Task<IActionResult> Download(int id, CancellationToken cancellationToken)
        {
            var item = await _context.DocumentArchiveItems
                .AsNoTracking()
                .FirstOrDefaultAsync(row => row.Id == id, cancellationToken);
            if (item == null)
            {
                return NotFound();
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            if (!await CanReadItemAsync(userId, item, cancellationToken))
            {
                return Forbid();
            }

            var physicalPath = _tenantPathResolver.MapRelativeToPhysical(Path.Combine(_environment.ContentRootPath, "App_Data"), item.RelativePath);
            if (!System.IO.File.Exists(physicalPath))
            {
                physicalPath = _tenantPathResolver.MapRelativeToPhysical(_environment.WebRootPath, item.RelativePath);
                if (!System.IO.File.Exists(physicalPath))
                {
                    return NotFound();
                }
            }

            var bytes = await System.IO.File.ReadAllBytesAsync(physicalPath, cancellationToken);
            return File(bytes, item.ContentType ?? "application/octet-stream", item.FileName);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Archive.Delete")]
        public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
        {
            var item = await _context.DocumentArchiveItems
                .FirstOrDefaultAsync(row => row.Id == id, cancellationToken);
            if (item == null)
            {
                return NotFound();
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            if (!await CanReadItemAsync(userId, item, cancellationToken))
            {
                return Forbid();
            }

            EnsureNotUnderLegalHold(item);

            var physicalPath = _tenantPathResolver.MapRelativeToPhysical(Path.Combine(_environment.ContentRootPath, "App_Data"), item.RelativePath);
            if (!System.IO.File.Exists(physicalPath))
            {
                physicalPath = _tenantPathResolver.MapRelativeToPhysical(_environment.WebRootPath, item.RelativePath);
            }
            _context.DocumentArchiveItems.Remove(item);
            await _context.SaveChangesAsync(cancellationToken);

            if (System.IO.File.Exists(physicalPath))
            {
                System.IO.File.Delete(physicalPath);
            }

            TempData["ArchiveMessage"] = "سند با موفقیت از بایگانی حذف شد.";
            return RedirectToAction(nameof(Index));
        }

        // ── Versioning Actions ───────────────────────────────

        [HttpGet]
        [PermissionAuthorize("Archive.View")]
        public async Task<IActionResult> Versions(int id, CancellationToken cancellationToken)
        {
            var item = await _context.DocumentArchiveItems
                .AsNoTracking()
                .FirstOrDefaultAsync(row => row.Id == id, cancellationToken);

            if (item == null) return NotFound();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            if (!await CanReadItemAsync(userId, item, cancellationToken))
                return Forbid();

            var versions = await _versioningService.GetVersionHistoryAsync(id, cancellationToken);

            var vm = new DocumentArchiveItemVM
            {
                Id = item.Id,
                Title = item.Title,
                Category = item.Category,
                AccessLevel = item.AccessLevel,
                FileName = item.FileName,
                RelativePath = item.RelativePath,
                ContentType = item.ContentType,
                FileSize = item.FileSize,
                IsPreviewable = item.IsPreviewable,
                CurrentVersion = item.CurrentVersion,
                ExpirationDate = item.ExpirationDate,
                IsArchived = item.IsArchived,
                CreatedAt = item.CreatedAt,
                CreatorName = item.CreatedByUser?.FullName ?? item.CreatedByUser?.UserName ?? "کاربر",
                Versions = versions.Select(v => new DocumentVersionVM
                {
                    Id = v.Id,
                    VersionNumber = v.VersionNumber,
                    ChangeSummary = v.ChangeSummary,
                    CreatorName = v.CreatedByUser?.FullName ?? v.CreatedByUser?.UserName ?? "کاربر",
                    CreatedAt = v.CreatedAt,
                    FileSize = v.FileSize
                }).ToList()
            };

            return View("Versions", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Archive.Create")]
        public async Task<IActionResult> UploadNewVersion(int id, DocumentVersionUploadVM model, CancellationToken cancellationToken)
        {
            var document = await _context.DocumentArchiveItems
                .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

            if (document == null) return NotFound();


            if (model.File != null)
            {
                try { ValidateUploadedFile(model.File); }
                catch (InvalidOperationException ex)
                {
                    TempData["ArchiveMessage"] = ex.Message;
                    return RedirectToAction(nameof(Versions), new { id });
                }
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            await _versioningService.CreateVersionAsync(document, model.File, model.ChangeSummary, userId, cancellationToken);

            TempData["ArchiveMessage"] = $"نسخه جدید ({document.CurrentVersion}) با موفقیت ثبت شد.";
            return RedirectToAction(nameof(Versions), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Archive.Edit")]
        public async Task<IActionResult> Rollback(int documentId, int versionId, CancellationToken cancellationToken)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            try
            {
                var document = await _versioningService.RollbackToVersionAsync(documentId, versionId, userId, cancellationToken);
                TempData["ArchiveMessage"] = $"سند با موفقیت به نسخه {document.CurrentVersion} بازگشت داده شد.";
            }
            catch (InvalidOperationException ex)
            {
                TempData["ArchiveMessage"] = ex.Message;
            }

            return RedirectToAction(nameof(Versions), new { id = documentId });
        }

        [HttpGet]
        [PermissionAuthorize("Archive.View")]
        public async Task<IActionResult> DownloadVersion(int versionId, CancellationToken cancellationToken)
        {
            var fileBytes = await _versioningService.GetVersionFileAsync(versionId, cancellationToken);
            if (fileBytes == null) return NotFound();

            var version = await _context.DocumentVersions
                .AsNoTracking()
                .FirstOrDefaultAsync(v => v.Id == versionId, cancellationToken);

            var fileName = version?.StoredFileName ?? "document";
            var contentType = version?.ContentType ?? "application/octet-stream";
            return File(fileBytes, contentType, fileName);
        }

        // ── Export Actions ───────────────────────────────

        [HttpGet]
        [PermissionAuthorize("Archive.View")]
        public async Task<IActionResult> ExportExcel(string? searchTerm, string? category, string? accessLevel, CancellationToken cancellationToken)
        {
            var exportService = HttpContext.RequestServices.GetRequiredService<IExportService>();
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

            var query = _context.DocumentArchiveItems.AsNoTracking().Include(item => item.CreatedByUser).AsQueryable();
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim();
                query = query.Where(item => item.Title.Contains(term) || item.FileName.Contains(term));
            }
            if (!string.IsNullOrWhiteSpace(category))
                query = query.Where(item => item.Category == category);
            if (!string.IsNullOrWhiteSpace(accessLevel))
                query = query.Where(item => item.AccessLevel == accessLevel);

            var rows = await query.OrderByDescending(item => item.CreatedAt).Take(5000).ToListAsync(cancellationToken);
            var visibleRows = new List<DocumentArchiveItem>();
            foreach (var row in rows)
            {
                if (await CanReadItemAsync(userId, row, cancellationToken))
                    visibleRows.Add(row);
            }

            var data = new DataTable("ArchiveDocuments");
            data.Columns.Add("شناسه");
            data.Columns.Add("عنوان");
            data.Columns.Add("دسته");
            data.Columns.Add("نام فایل");
            data.Columns.Add("حجم (KB)");
            data.Columns.Add("ایجاد کننده");
            data.Columns.Add("تاریخ");

            foreach (var item in visibleRows)
            {
                data.Rows.Add(item.Id, item.Title, item.Category, item.FileName,
                    Math.Round(item.FileSize / 1024.0, 1),
                    item.CreatedByUser?.FullName ?? item.CreatedByUser?.UserName ?? "-",
                    item.CreatedAt.ToString("yyyy/MM/dd"));
            }

            var bytes = exportService.ExportToExcel(data, "اسناد بایگانی");
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "ArchiveDocuments.xlsx");
        }

        [HttpGet]
        [PermissionAuthorize("Archive.View")]
        public async Task<IActionResult> ExportPdf(string? searchTerm, string? category, string? accessLevel, CancellationToken cancellationToken)
        {
            var exportService = HttpContext.RequestServices.GetRequiredService<IExportService>();
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

            var query = _context.DocumentArchiveItems.AsNoTracking().Include(item => item.CreatedByUser).AsQueryable();
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim();
                query = query.Where(item => item.Title.Contains(term) || item.FileName.Contains(term));
            }
            if (!string.IsNullOrWhiteSpace(category))
                query = query.Where(item => item.Category == category);
            if (!string.IsNullOrWhiteSpace(accessLevel))
                query = query.Where(item => item.AccessLevel == accessLevel);

            var rows = await query.OrderByDescending(item => item.CreatedAt).Take(5000).ToListAsync(cancellationToken);
            var visibleRows = new List<DocumentArchiveItem>();
            foreach (var row in rows)
            {
                if (await CanReadItemAsync(userId, row, cancellationToken))
                    visibleRows.Add(row);
            }

            var data = new DataTable("ArchiveDocuments");
            data.Columns.Add("شناسه");
            data.Columns.Add("عنوان");
            data.Columns.Add("دسته");
            data.Columns.Add("نام فایل");
            data.Columns.Add("حجم (KB)");
            data.Columns.Add("ایجاد کننده");
            data.Columns.Add("تاریخ");

            foreach (var item in visibleRows)
            {
                data.Rows.Add(item.Id, item.Title, item.Category, item.FileName,
                    Math.Round(item.FileSize / 1024.0, 1),
                    item.CreatedByUser?.FullName ?? item.CreatedByUser?.UserName ?? "-",
                    item.CreatedAt.ToString("yyyy/MM/dd"));
            }

            var bytes = exportService.ExportToPdf(data, "اسناد بایگانی");
            return File(bytes, "text/html; charset=utf-8", "ArchiveDocuments.html");
        }

        private static bool IsPreviewableContentType(string contentType)
        {
            return contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ||
                   contentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase) ||
                   contentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase);
        }

        private async Task<bool> CanReadItemAsync(string userId, DocumentArchiveItem item, CancellationToken cancellationToken)
        {
            if (string.Equals(item.AccessLevel, DocumentArchiveVisibilityLevels.Public, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (string.Equals(item.AccessLevel, DocumentArchiveVisibilityLevels.Restricted, StringComparison.OrdinalIgnoreCase))
            {
                return await _permissionAccessService.UserHasPermissionAsync(userId, "Archive.ViewSensitive", cancellationToken);
            }

            return item.Category switch
            {
                DocumentArchiveModules.Letters => await _permissionAccessService.UserHasPermissionAsync(userId, "Letters.Read", cancellationToken),
                DocumentArchiveModules.Invoices => await _permissionAccessService.UserHasPermissionAsync(userId, "Finance.View", cancellationToken),
                DocumentArchiveModules.Personnel => await _permissionAccessService.UserHasPermissionAsync(userId, "HR.View", cancellationToken),
                DocumentArchiveModules.Contracts => await _permissionAccessService.UserHasPermissionAsync(userId, "HR.View", cancellationToken),
                DocumentArchiveModules.Insurance => await _permissionAccessService.UserHasPermissionAsync(userId, "HR.View", cancellationToken),
                DocumentArchiveModules.Warehouse => await _permissionAccessService.UserHasPermissionAsync(userId, "Warehouse.View", cancellationToken),
                _ => true
            };
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

        private const long MaxFileBytes = 20 * 1024 * 1024; // 20 MB

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

        /// <summary>
        /// جلوگیری از تغییر/حذف اسنادی که تحت نگهداری قانونی (پرونده قضایی) قرار دارند.
        /// </summary>
        public static void EnsureNotUnderLegalHold(DocumentArchiveItem item)
        {
            if (item.IsUnderLegalHold)
            {
                var reason = string.IsNullOrWhiteSpace(item.HoldReason) ? "دلیل ثبت نشده است" : item.HoldReason;
                throw new InvalidOperationException(
                    $"این سند تحت فرآیندهای قانونی قرار دارد و امکان تغییر یا حذف آن وجود ندارد. ({reason})");
            }
        }

    }
}
