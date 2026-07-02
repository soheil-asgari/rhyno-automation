using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Models;
using OfficeAutomation.Modules.Workflow.Infrastructure.Persistence;
using OfficeAutomation.Services.Security;
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

        public DocumentArchiveController(
            IWorkflowDbContext context,
            IWebHostEnvironment environment,
            IPermissionAccessService permissionAccessService,
            Services.Tenancy.ITenantPathResolver tenantPathResolver)
        {
            _context = context;
            _environment = environment;
            _permissionAccessService = permissionAccessService;
            _tenantPathResolver = tenantPathResolver;
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
                    IsUnderLegalHold = item.IsUnderLegalHold,
                    HoldReason = item.HoldReason,
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
            if (!ModelState.IsValid || model.File == null || model.File.Length == 0)
            {
                TempData["ArchiveMessage"] = "فایل انتخابی معتبر نیست.";
                return RedirectToAction(nameof(Index));
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            var safeTitle = model.Title.Trim();
            var extension = Path.GetExtension(model.File.FileName);
            var storedFileName = $"{Guid.NewGuid():N}{extension}";
            var uploadRoot = _tenantPathResolver.GetArchiveRoot(_environment.WebRootPath);
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

            return Redirect(item.RelativePath);
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

            var physicalPath = _tenantPathResolver.MapRelativeToPhysical(_environment.WebRootPath, item.RelativePath);
            if (!System.IO.File.Exists(physicalPath))
            {
                return NotFound();
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

            try
            {
                EnsureNotUnderLegalHold(item);
            }
            catch (InvalidOperationException error)
            {
                TempData["ArchiveMessage"] = error.Message;
                return BadRequest(error.Message);
            }

            var physicalPath = _tenantPathResolver.MapRelativeToPhysical(_environment.WebRootPath, item.RelativePath);
            _context.DocumentArchiveItems.Remove(item);
            await _context.SaveChangesAsync(cancellationToken);

            if (System.IO.File.Exists(physicalPath))
            {
                System.IO.File.Delete(physicalPath);
            }

            TempData["ArchiveMessage"] = "سند با موفقیت از بایگانی حذف شد.";
            return RedirectToAction(nameof(Index));
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

        public static void EnsureNotUnderLegalHold(DocumentArchiveItem item)
        {
            if (!item.IsUnderLegalHold)
            {
                return;
            }

            var reason = string.IsNullOrWhiteSpace(item.HoldReason)
                ? "این سند تحت نگهداری قانونی است."
                : item.HoldReason.Trim();
            throw new InvalidOperationException(
                $"امکان حذف یا ویرایش سند به دلیل فرآیندهای قانونی وجود ندارد. دلیل نگهداری: {reason}");
        }
    }
}
