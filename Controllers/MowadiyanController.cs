using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Filters;
using OfficeAutomation.Modules.MowadiyanIntegration;
using OfficeAutomation.Modules.Persistence;
using OfficeAutomation.Modules.Persistence.Entities;
using OfficeAutomation.Modules.Reconciliation;
using OfficeAutomation.Utilities;

namespace OfficeAutomation.Controllers;

/// <summary>
/// Mowadiyan (سامانه مودیان) integration &amp; reconciliation UI — Phase 3.
/// </summary>
[Authorize]
[RequireAccessArea("Finance")]
public class MowadiyanController : Controller
{
    private readonly IDbContextFactory<TaxDbContext> _dbFactory;
    private readonly IMowadiyanSubmissionService _submissionService;
    private readonly IInvoiceReconciliationService _reconciliationService;
    private readonly IMowadiyanPortalSyncService _portalSyncService;
    private readonly ILogger<MowadiyanController> _logger;

    public MowadiyanController(
        IDbContextFactory<TaxDbContext> dbFactory,
        IMowadiyanSubmissionService submissionService,
        IInvoiceReconciliationService reconciliationService,
        IMowadiyanPortalSyncService portalSyncService,
        ILogger<MowadiyanController> logger)
    {
        _dbFactory = dbFactory;
        _submissionService = submissionService;
        _reconciliationService = reconciliationService;
        _portalSyncService = portalSyncService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var summary = await _submissionService.GetSummaryAsync(cancellationToken);
        ViewBag.Summary = summary;

        using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var recent = await db.MowadiyanSubmissions
            .Include(s => s.InvoiceRecord)
            .OrderByDescending(s => s.CreatedAt)
            .Take(50)
            .ToListAsync(cancellationToken);
        ViewBag.RecentSubmissions = recent;

        var settings = await db.MowadiyanSettings
            .OrderByDescending(s => s.UpdatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        ViewBag.IsSandbox = settings?.IsSandbox ?? true;

        return Request.IsRhynoTabOrAjax() ? PartialView() : View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BatchSend(CancellationToken cancellationToken)
    {
        var count = await _submissionService.EnqueuePendingAsync(cancellationToken);
        _logger.LogInformation("Batch send triggered: {Count} invoices enqueued.", count);
        TempData["AlertMessage"] = count > 0
            ? $"{count} فاکتور در صف ارسال قرار گرفت."
            : "فاکتور در انتظار ارسالی در صف وجود ندارد.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RetryFailed(CancellationToken cancellationToken)
    {
        var count = await _submissionService.RetryFailedAsync(cancellationToken);
        TempData["AlertMessage"] = count > 0
            ? $"{count} فاکتور ناموفق مجدداً در صف ارسال قرار گرفت."
            : "ارسال ناموفقی برای تلاش مجدد وجود ندارد.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Reconciliation(CancellationToken cancellationToken)
    {
        using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var results = await db.InvoiceReconciliationResults
            .Include(r => r.InvoiceRecord)
            .Include(r => r.Discrepancies)
            .OrderByDescending(r => r.CreatedAt)
            .Take(100)
            .ToListAsync(cancellationToken);
        ViewBag.Results = results;

        var settings = await db.MowadiyanSettings
            .OrderByDescending(s => s.UpdatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        ViewBag.HasPortalSettings = settings is not null
                                    && !string.IsNullOrWhiteSpace(settings.PortalUsername)
                                    && !string.IsNullOrWhiteSpace(settings.PortalPassword);
        ViewBag.IsSandbox = settings?.IsSandbox ?? true;

        return Request.IsRhynoTabOrAjax() ? PartialView() : View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RunReconciliation(CancellationToken cancellationToken)
    {
        var count = await _reconciliationService.ReconcileAllAsync(cancellationToken);
        TempData["AlertMessage"] = count > 0
            ? $"{count} فاکتور با سامانه مودیان تطبیق داده شد."
            : "فاکتوری برای تطبیق وجود ندارد.";
        return RedirectToAction(nameof(Reconciliation));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreatePortalExport([FromBody] MowadiyanPortalExportRequest request)
    {
        if (request is null || request.TaxYear < 1390 || request.TaxYear > 1500 || request.TaxPeriod < 1 || request.TaxPeriod > 4)
        {
            return Json(new { ok = false, message = "سال مالیاتی یا دوره انتخاب‌شده نامعتبر است." });
        }

        try
        {
            var jobId = await _portalSyncService.StartExportAsync(request, Request.HttpContext.RequestAborted);
            return Json(new { ok = true, jobId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start Mowadiyan portal export.");
            return Json(new { ok = false, message = ex.Message });
        }
    }

    /// <summary>
    /// Submits the saved portal username/password to tp.tax.gov.ir, which triggers the SMS OTP to
    /// the user's phone, and returns the active session/challenge token for the next step.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> InitiatePortalLogin(CancellationToken cancellationToken)
    {
        try
        {
            var result = await _portalSyncService.InitiatePortalLoginAsync(cancellationToken);
            return Json(new
            {
                ok = result.Success,
                message = result.Message,
                challengeToken = result.ChallengeToken,
                isSandbox = result.IsSandbox,
                maskedPhone = result.MaskedPhone,
                status = result.Status,
                alreadyLoggedIn = result.AlreadyLoggedIn
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initiate Mowadiyan portal login.");
            return Json(new { ok = false, message = ex.Message });
        }
    }

    [HttpGet]
    public IActionResult ExportProgress(Guid jobId)
    {
        var progress = _portalSyncService.GetProgress(jobId);
        if (progress is null)
        {
            return Json(new { ok = false, message = "وضعیت جاب یافت نشد." });
        }

        return Json(new
        {
            ok = true,
            jobId = progress.JobId,
            stage = progress.Stage.ToString(),
            stageText = progress.StageText,
            message = progress.Message,
            pollElapsedSeconds = progress.PollElapsedSeconds,
            totalRows = progress.TotalRows,
            importedRows = progress.ImportedRows,
            downloadUrl = progress.DownloadUrl,
            error = progress.Error,
            isComplete = progress.IsComplete,
            isFailed = progress.IsFailed
        });
    }

    [HttpGet]
    public async Task<IActionResult> Settings(CancellationToken cancellationToken)
    {
        using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var settings = await db.MowadiyanSettings
            .OrderByDescending(s => s.UpdatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return Request.IsRhynoTabOrAjax() ? PartialView(settings ?? new MowadiyanSettings()) : View(settings ?? new MowadiyanSettings());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveSettings(MowadiyanSettings model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return Request.IsRhynoTabOrAjax() ? PartialView(nameof(Settings), model) : View(nameof(Settings), model);
        }

        try
        {
            // Defensive clamp to the configured column sizes so over-long credentials
            // never surface as a DbUpdateException ("String or binary data would be truncated")
            // and take the whole endpoint down with an HTTP 500.
            model.PortalUsername = Clamp(model.PortalUsername, 200) ?? string.Empty;
            model.PortalPassword = Clamp(model.PortalPassword, 500) ?? string.Empty;

            using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

            var existing = await db.MowadiyanSettings
                .OrderByDescending(s => s.UpdatedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (existing is null)
            {
                model.Id = Guid.NewGuid();
                model.UpdatedAt = DateTime.Now;
                db.MowadiyanSettings.Add(model);
            }
            else
            {
                existing.ClientId = model.ClientId;
                existing.MemorySecret = model.MemorySecret;
                existing.PrivateKeyPath = model.PrivateKeyPath;
                existing.PrivateKeyPassword = model.PrivateKeyPassword;
                existing.EconomicCode = model.EconomicCode;
                existing.NationalId = model.NationalId;
                existing.PortalUsername = model.PortalUsername;
                existing.PortalPassword = model.PortalPassword;
                existing.BaseUrl = model.BaseUrl;
                existing.IsSandbox = model.IsSandbox;
                existing.UpdatedAt = DateTime.Now;
            }

            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save Mowadiyan settings (PortalUsername/PortalPassword).");

            // Return a friendly JSON error for tab/AJAX requests instead of an unhandled HTTP 500.
            if (Request.IsRhynoTabOrAjax())
            {
                return Json(new { success = false, message = ex.Message });
            }

            TempData["AlertMessage"] = $"خطا در ذخیره تنظیمات سامانه مودیان: {ex.Message}";
            return View(nameof(Settings), model);
        }

        // When the request is a Rhyno tab load or an AJAX/POST call (e.g. the AJAX
        // submit handler in Settings.cshtml), return JSON so the client can show a
        // toast without triggering a full page (GET) navigation.
        if (Request.IsRhynoTabOrAjax())
        {
            return Json(new { success = true, message = "تنظیمات با موفقیت ذخیره شد" });
        }

        TempData["AlertMessage"] = "تنظیمات سامانه مودیان با موفقیت ذخیره شد.";
        return RedirectToAction(nameof(Settings));
    }

    private static string? Clamp(string? value, int maxLength)
    {
        if (value is null)
        {
            return value;
        }

        return value.Length <= maxLength ? value : value[..maxLength];
    }
}