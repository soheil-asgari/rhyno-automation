using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OfficeAutomation.Filters;
using OfficeAutomation.Modules.InvoiceIntake;
using OfficeAutomation.Utilities;

namespace OfficeAutomation.Controllers
{
    /// <summary>
    /// Tax &amp; Invoice Intake Module — Phase 1.
    /// Serves the drag &amp; drop intake UI and the JSON API used by it.
    /// </summary>
    [Authorize]
    [RequireAccessArea("Finance")]
    public class InvoiceIntakeController : Controller
    {
        private readonly InvoiceIntakeService _service;

        public InvoiceIntakeController(InvoiceIntakeService service)
        {
            _service = service;
        }

        [HttpGet]
        public IActionResult Index()
        {
            ViewBag.MaxFiles = InvoiceIntakeService.MaxFilesPerOwner;
            ViewBag.MaxFileBytes = _service.MaxFileBytes;
            ViewBag.MaxFileMb = _service.MaxFileBytes / (1024 * 1024);
            ViewBag.AllowedExtensions = new[] { ".png", ".jpg", ".jpeg", ".pdf" };
            // When the page is loaded inside an ERP tab (AJAX / dedicated tab header),
            // render only the view content without the outer shell layout.
            return Request.IsRhynoTabOrAjax() ? PartialView() : View();
        }

        /// <summary>
        /// Validates and stores uploaded files (multipart form field name: "files").
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Upload(IFormFileCollection files)
        {
            var ownerKey = ResolveOwnerKey();
            var uploaded = new List<InvoiceIntakeItemDto>();
            var errors = new List<string>();

            if (files is null || files.Count == 0)
            {
                return Json(new { success = false, message = "فایلی برای بارگذاری انتخاب نشده است.", items = uploaded, errors });
            }

            if (files.Count > InvoiceIntakeService.MaxFilesPerOwner)
            {
                return Json(new { success = false, message = $"حداکثر {InvoiceIntakeService.MaxFilesPerOwner} فایل در هر بار مجاز است.", items = uploaded, errors });
            }

            foreach (var file in files)
            {
                var item = _service.ValidateAndStore(file, ownerKey, out var error);
                if (item is not null)
                {
                    uploaded.Add(_service.ToDto(item));
                }
                else
                {
                    errors.Add($"{file.FileName}: {error}");
                }
            }

            var anySuccess = uploaded.Count > 0;
            var message = uploaded.Count switch
            {
                0 => errors.FirstOrDefault() ?? "هیچ فایلی بارگذاری نشد.",
                1 => "۱ فایل با موفقیت بارگذاری شد.",
                _ => $"{uploaded.Count} فایل با موفقیت بارگذاری شد."
            };

            return Json(new
            {
                success = anySuccess,
                message,
                items = uploaded,
                errors,
                total = _service.List(ownerKey).Count
            });
        }

        [HttpGet]
        public IActionResult List()
        {
            var ownerKey = ResolveOwnerKey();
            var items = _service.List(ownerKey).Select(_service.ToDto).ToList();
            return Json(new { success = true, items });
        }

        /// <summary>
        /// Runs the OCR pipeline (extraction + schema validation) for one stored file.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Process(Guid id, CancellationToken cancellationToken)
        {
            var result = await _service.ProcessAsync(id, ResolveOwnerKey(), cancellationToken);
            return Json(new { success = result.Success, message = result.Message, item = result.Item });
        }

        /// <summary>
        /// Saves user-edited metadata for a stored file and re-validates against the schema.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult UpdateMetadata(Guid id, [FromBody] UpdateMetadataRequest request)
        {
            var result = _service.UpdateMetadata(id, ResolveOwnerKey(), request);
            return Json(new { success = result.Success, message = result.Message, item = result.Item });
        }

        /// <summary>
        /// Removes a stored file and releases its intake slot.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Delete(Guid id)
        {
            var deleted = _service.Delete(id, ResolveOwnerKey());
            return Json(new { success = deleted, message = deleted ? "سند حذف شد." : "سند یافت نشد." });
        }

        private string ResolveOwnerKey() =>
            User.FindFirstValue(ClaimTypes.NameIdentifier) ?? $"{HttpContext.Connection.RemoteIpAddress}";
    }
}
