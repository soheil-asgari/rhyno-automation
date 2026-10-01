using OfficeAutomation.Services.Licensing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OfficeAutomation.Filters;
using OfficeAutomation.Modules.IntakeAutomation;
using OfficeAutomation.Modules.InvoiceIntake;

namespace OfficeAutomation.Controllers;

/// <summary>
/// UI for smart intake automation — bulk ZIP upload and webhook connector overview.
/// </summary>
[Authorize]
[RequireAccessArea("Finance")]
[RequiresModule(LicensableModules.Financial)]
public sealed class IntakeAutomationController : Controller
{
    private readonly ZipArchiveProcessor _zipProcessor;
    private readonly InvoiceIntakeService _intakeService;
    private readonly ILogger<IntakeAutomationController> _logger;

    public IntakeAutomationController(
        ZipArchiveProcessor zipProcessor,
        InvoiceIntakeService intakeService,
        ILogger<IntakeAutomationController> logger)
    {
        _zipProcessor = zipProcessor;
        _intakeService = intakeService;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Index()
    {
        ViewBag.MaxEntries = ZipArchiveProcessor.DefaultMaxEntries;
        ViewBag.MaxZipMb = ZipArchiveProcessor.DefaultMaxZipBytes / (1024 * 1024);
        return View();
    }

    /// <summary>Handles a bulk ZIP upload and auto-queues its entries.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadZip(IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            TempData["AlertType"] = "error";
            TempData["AlertMessage"] = "فایل ZIP انتخاب نشده است.";
            return RedirectToAction(nameof(Index));
        }

        var ownerKey = ResolveOwnerKey();
        await using var stream = file.OpenReadStream();
        var result = await _zipProcessor.ProcessZipAsync(stream, ownerKey, cancellationToken);

        TempData["AlertType"] = result.Success ? "success" : "error";
        TempData["AlertMessage"] = result.Message;

        if (result.Errors.Count > 0)
        {
            TempData["ZipErrors"] = result.Errors;
        }

        _logger.LogInformation(
            "ZIP upload processed by {User}: stored={Stored}, processed={Processed}, rejected={Rejected}.",
            ownerKey, result.StoredCount, result.ProcessedCount, result.RejectedCount);

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public IActionResult List()
    {
        var items = _intakeService.List(ResolveOwnerKey()).Select(_intakeService.ToDto).ToList();
        return Json(new { success = true, items });
    }

    private string ResolveOwnerKey() =>
        User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "anonymous";
}