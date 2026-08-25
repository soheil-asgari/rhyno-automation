using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OfficeAutomation.Models;
using OfficeAutomation.Services.Contracts;
using OfficeAutomation.Services.Security;
using System.Security.Claims;

namespace OfficeAutomation.Controllers;

[Authorize]
[PermissionAuthorize("Contract.View")]
public class DocumentRequirementsController : Controller
{
    private readonly IDocumentRequirementService _requirementService;
    private readonly IContractService _contractService;
    private readonly ILogger<DocumentRequirementsController> _logger;

    public DocumentRequirementsController(
        IDocumentRequirementService requirementService,
        IContractService contractService,
        ILogger<DocumentRequirementsController> logger)
    {
        _requirementService = requirementService;
        _contractService = contractService;
        _logger = logger;
    }

    /// <summary>GET: /Contracts/{contractId}/Requirements — returns checklist partial view</summary>
    [HttpGet("Contracts/{contractId}/Requirements")]
    public async Task<IActionResult> Index(int contractId, CancellationToken ct)
    {
        try
        {
            var vm = await _requirementService.GetChecklistAsync(contractId, ct);
            return PartialView("_DocumentRequirements", vm);
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    /// <summary>GET: /Contracts/{contractId}/Requirements/Check — returns JSON with missing docs info</summary>
    [HttpGet("Contracts/{contractId}/Requirements/Check")]
    public async Task<IActionResult> Check(int contractId, CancellationToken ct)
    {
        try
        {
            var (allUploaded, missingNames) = await _requirementService.ValidateRequiredDocumentsAsync(contractId, ct);
            return Json(new { allUploaded, missingNames });
        }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    /// <summary>POST: /Contracts/{contractId}/Requirements/Seed — seed default requirements for the contract type</summary>
    [HttpPost("Contracts/{contractId}/Requirements/Seed")]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize("Contract.Edit")]
    public async Task<IActionResult> SeedDefaults(int contractId, CancellationToken ct)
    {
        try
        {
            // Get contract to determine its type
            var contract = await _contractService.GetContractByIdAsync(contractId, GetCurrentUserId(), ct);
            if (contract == null || string.IsNullOrEmpty(contract.ContractType))
                return Json(new { success = false, message = "نوع قرارداد مشخص نیست." });

            var count = await _requirementService.SeedDefaultRequirementsAsync(contract.ContractType, ct);
            return Json(new { success = true, count });
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (UnauthorizedAccessException) { return Json(new { success = false, message = "دسترسی ندارید." }); }
    }

    private string GetCurrentUserId()
    {
        var userId = User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            throw new UnauthorizedAccessException("کاربر احراز هویت نشده است.");
        return userId;
    }
}