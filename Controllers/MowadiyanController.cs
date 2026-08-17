using System.Collections.Concurrent;
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
    private readonly ITaxExporterService _taxExporterService;
    private readonly ILogger<MowadiyanController> _logger;

    // In-memory correlation of portal-export jobs so the existing front-end progress
    // polling (ExportProgress) keeps working now that the refactored TaxExporterService
    // runs each CompleteAsync step synchronously within the OTP/export request.
    private static readonly ConcurrentDictionary<Guid, PortalExportJob> Jobs = new();

    /// <summary>Most recent portal session token so the OTP/export step can complete
    /// the session started by InitiatePortalLogin even when the browser never echoes it.</summary>
    private static string? LastSessionToken;

    public MowadiyanController(
        IDbContextFactory<TaxDbContext> dbFactory,
        IMowadiyanSubmissionService submissionService,
        IInvoiceReconciliationService reconciliationService,
        ITaxExporterService taxExporterService,
        ILogger<MowadiyanController> logger)
    {
        _dbFactory = dbFactory;
        _submissionService = submissionService;
        _reconciliationService = reconciliationService;
        _taxExporterService = taxExporterService;
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

        var job = new PortalExportJob();
        Jobs[job.JobId] = job;

        try
        {
            // The refactored exporter completes the entire OTP + export + download in one
            // call, keyed by the session token produced by InitiatePortalLogin (the request
            // echoes it back, otherwise we fall back to the most recent initiated session).
            try
            {
                job.SetStage(PortalExportStage.Authenticating, "ورود به پرتال با کد پیامکی...");
                // Prefer the session token the client echoes from InitiatePortalLogin; otherwise
                // fall back to the most recently initiated session token captured in this process.
                var sessionToken = !string.IsNullOrWhiteSpace(request.SessionToken)
                    ? request.SessionToken
                    : (LastSessionToken ?? string.Empty);
                var result = await _taxExporterService.CompleteAsync(
                    sessionToken: sessionToken,
                    otpCode: request.OtpCode,
                    taxYear: request.TaxYear,
                    taxPeriod: request.TaxPeriod,
                    cancellationToken: Request.HttpContext.RequestAborted);

                if (result.Success)
                {
                    job.StageText = "Completed";
                    job.Message = result.Message ?? "دانلود فایل خروجی با موفقیت انجام شد.";
                    job.DownloadUrl = result.OutputPath ?? string.Empty;
                    job.IsComplete = true;
                    // The session has been fully used; drop the remembered token so a later
                    // unrelated operation never reuses this stale session.
                    LastSessionToken = null;
                }
                else
                {
                    job.StageText = "Failed";
                    job.Message = result.Message ?? "تکمیل عملیات خروجی ناموفق بود.";
                    job.Error = result.Message ?? string.Empty;
                    job.IsFailed = true;
                }
            }
            catch (Exception innerEx)
            {
                _logger.LogError(innerEx, "Failed to complete Mowadiyan portal export.");
                job.StageText = "Failed";
                job.Message = innerEx.Message;
                job.Error = innerEx.Message;
                job.IsFailed = true;
            }

            return Json(new { ok = true, jobId = job.JobId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start Mowadiyan portal export.");
            job.StageText = "Failed";
            job.Message = ex.Message;
            job.Error = ex.Message;
            job.IsFailed = true;
            return Json(new { ok = true, jobId = job.JobId });
        }
    }

    /// <summary>
    /// Submits the saved portal username/password to tp.tax.gov.ir, which triggers the SMS OTP to
    /// the user's phone, and returns the correlation session token for the next step. The new
    /// refactored exporter keeps the SSO session alive in a persistent Chromium profile so the
    /// OTP/export step (<see cref="CreatePortalExport"/>) can complete the same session.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> InitiatePortalLogin([FromBody] MowadiyanPortalExportRequest? request, CancellationToken cancellationToken)
    {
        try
        {
            var parameters = await BuildExportParametersAsync(request ?? new MowadiyanPortalExportRequest(), cancellationToken);
            var result = await _taxExporterService.InitiateAsync(parameters, cancellationToken);

            LastSessionToken = result.SessionToken;

            return Json(new
            {
                ok = result.Success,
                message = result.Message,
                challengeToken = result.SessionToken,
                isSandbox = false,
                maskedPhone = result.MaskedPhone,
                status = result.Status.ToString(),
                alreadyLoggedIn = result.Status == TaxExportInitiationStatus.AlreadyLoggedIn,
                awaitingOtp = result.Status == TaxExportInitiationStatus.AwaitingOtp,
                requiresOtp = result.RequiresOtp
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
        if (!Jobs.TryGetValue(jobId, out var progress))
        {
            return Json(new { ok = false, message = "وضعیت جاب یافت نشد." });
        }

        return Json(new
        {
            ok = true,
            jobId = progress.JobId,
            stage = progress.Stage,
            stageText = progress.StageText,
            message = progress.Message,
            pollElapsedSeconds = progress.PollElapsedSeconds,
            totalRows = (int?)0,
            importedRows = 0,
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

    private async Task<TaxExportParameters> BuildExportParametersAsync(
        MowadiyanPortalExportRequest request, CancellationToken cancellationToken)
    {
        using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var settings = await db.MowadiyanSettings
            .OrderByDescending(s => s.UpdatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var username = !string.IsNullOrWhiteSpace(settings?.PortalUsername)
            ? settings!.PortalUsername
            : settings?.NationalId ?? string.Empty;

        return new TaxExportParameters
        {
            Username = username,
            Password = settings?.PortalPassword ?? string.Empty,
            OtpCode = request?.OtpCode ?? string.Empty,
            TaxYear = request?.TaxYear ?? 0,
            TaxPeriod = request?.TaxPeriod ?? 0,
            // Let TaxExporterService resolve its default output directory.
            OutputDirectory = string.Empty
        };
    }
}

/// <summary>Mid-stage label surfaced to the front-end progress polling.</summary>
internal enum PortalExportStage
{
    Authenticating = 1,
    Submitting = 2,
    Completed = 6,
    Failed = 7
}

/// <summary>In-memory progress record for a single portal-export job, kept compatible
/// with the front-end <c>ExportProgress</c> polling contract.</summary>
internal sealed class PortalExportJob
{
    public Guid JobId { get; } = Guid.NewGuid();
    public PortalExportStage Stage { get; set; } = PortalExportStage.Submitting;
    public string StageText { get; set; } = "Submitting";
    public string Message { get; set; } = "در حال تکمیل عملیات خروجی...";
    public string Error { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;
    public bool IsComplete { get; set; }
    public bool IsFailed { get; set; }
    public DateTime StartedAt { get; } = DateTime.UtcNow;
    public int PollElapsedSeconds => (int)(DateTime.UtcNow - StartedAt).TotalSeconds;

    public void SetStage(PortalExportStage stage, string message)
    {
        Stage = stage;
        StageText = stage.ToString();
        Message = message;
    }
}