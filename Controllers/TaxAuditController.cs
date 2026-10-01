using OfficeAutomation.Services.Licensing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OfficeAutomation.Filters;
using OfficeAutomation.Modules.SecurityAndAudit;

namespace OfficeAutomation.Controllers;

/// <summary>
/// Audit trail viewer for the tax module. Restricted to Admin and Auditor roles.
/// </summary>
[Authorize]
[RequireAccessArea("Finance")]
[RequiresModule(LicensableModules.Financial)]
public sealed class TaxAuditController : Controller
{
    private readonly ITaxAuditService _audit;
    private readonly ILogger<TaxAuditController> _logger;

    public TaxAuditController(ITaxAuditService audit, ILogger<TaxAuditController> logger)
    {
        _audit = audit;
        _logger = logger;
    }

    [HttpGet]
    [Authorize(Roles = TaxRbacRoles.AuditReaders)]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var entries = await _audit.GetRecentAsync(200, cancellationToken);
        ViewBag.Entries = entries;
        return View();
    }

    [HttpGet]
    [Authorize(Roles = TaxRbacRoles.AuditReaders)]
    public async Task<IActionResult> Search(string? action = null, string? search = null, CancellationToken cancellationToken = default)
    {
        var entries = await _audit.QueryAsync(action, search, 200, cancellationToken);
        return Json(entries.Select(e => new
        {
            e.Id, e.UserId, e.Action, e.EntityName, e.EntityId, e.DateTime, e.Details, e.UserIP
        }));
    }
}