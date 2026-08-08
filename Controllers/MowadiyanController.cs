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
    private readonly ILogger<MowadiyanController> _logger;

    public MowadiyanController(
        IDbContextFactory<TaxDbContext> dbFactory,
        IMowadiyanSubmissionService submissionService,
        IInvoiceReconciliationService reconciliationService,
        ILogger<MowadiyanController> logger)
    {
        _dbFactory = dbFactory;
        _submissionService = submissionService;
        _reconciliationService = reconciliationService;
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
            existing.BaseUrl = model.BaseUrl;
            existing.IsSandbox = model.IsSandbox;
            existing.UpdatedAt = DateTime.Now;
        }

        await db.SaveChangesAsync(cancellationToken);
        TempData["AlertMessage"] = "تنظیمات سامانه مودیان با موفقیت ذخیره شد.";
        return RedirectToAction(nameof(Settings));
    }
}