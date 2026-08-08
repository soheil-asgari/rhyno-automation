using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OfficeAutomation.Filters;
using OfficeAutomation.Modules.TaxAnalytics;
using OfficeAutomation.Modules.SecurityAndAudit;
using OfficeAutomation.Utilities;

namespace OfficeAutomation.Controllers;

/// <summary>
/// AI Tax Assistant &amp; Smart Analytics — Phase 4.
/// Serves the insights dashboard and the Rahkaran-styled slide-over AI assistant.
/// </summary>
[Authorize]
[RequireAccessArea("Finance")]
[Route("[controller]")]
public sealed class TaxAnalyticsController : Controller
{
    private readonly ITaxInsightsService _insights;
    private readonly ITaxQueryService _query;
    private readonly ITaxAuditService _audit;
    private readonly ILogger<TaxAnalyticsController> _logger;

    public TaxAnalyticsController(
        ITaxInsightsService insights,
        ITaxQueryService query,
        ITaxAuditService audit,
        ILogger<TaxAnalyticsController> logger)
    {
        _insights = insights;
        _query = query;
        _audit = audit;
        _logger = logger;
    }

    /// <summary>Default route redirects to the Insights dashboard.</summary>
    [HttpGet]
    [Route("")]
    [Route("Index")]
    [Authorize(Roles = TaxRbacRoles.AnalyticsViewers)]
    public IActionResult Index()
    {
        return RedirectToAction(nameof(Insights));
    }

    /// <summary>Insights dashboard (Admin, Accountant, Auditor).</summary>
    [HttpGet("Insights")]
    [Authorize(Roles = TaxRbacRoles.AnalyticsViewers)]
    public async Task<IActionResult> Insights(CancellationToken cancellationToken)
    {
        var snapshot = await _insights.BuildSnapshotAsync(cancellationToken);
        ViewBag.Snapshot = snapshot;

        await _audit.RecordAsync(
            TaxAuditActions.InsightGenerated,
            "TaxAnalytics",
            entityId: null,
            details: $"Generated {snapshot.Alerts.Count} tax insight(s).",
            userId: ResolveUserId(),
            userIp: HttpContext.Connection.RemoteIpAddress?.ToString(),
            cancellationToken);

        return Request.IsRhynoTabOrAjax() ? PartialView() : View();
    }

    /// <summary>AI assistant slide-over page.</summary>
    [HttpGet("Assistant")]
    [Authorize(Roles = TaxRbacRoles.AnalyticsViewers)]
    public IActionResult Assistant()
    {
        return Request.IsRhynoTabOrAjax() ? PartialView() : View();
    }

    /// <summary>Answers a natural-language tax query from the assistant UI.</summary>
    [HttpPost("Ask")]
    [Authorize(Roles = TaxRbacRoles.AnalyticsViewers)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Ask([FromBody] TaxAssistantQuestion request, CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Question))
        {
            return Json(new { success = false, answer = "سوال خود را وارد کنید." });
        }

        var result = await _query.AnswerAsync(request.Question, cancellationToken);
        return Json(new { success = result.Success, answer = result.Answer, data = result.DataPoints });
    }

    /// <summary>Returns the current anomaly alerts (used by the widget).</summary>
    [HttpGet("Alerts")]
    [Authorize(Roles = TaxRbacRoles.AnalyticsViewers)]
    public async Task<IActionResult> Alerts(CancellationToken cancellationToken)
    {
        var alerts = await _insights.GenerateAlertsAsync(cancellationToken);
        return Json(alerts.Select(a => new
        {
            a.Code, severity = a.Severity.ToString(), a.Title, a.Message, a.MetricValue
        }));
    }

    private string ResolveUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "system";
}