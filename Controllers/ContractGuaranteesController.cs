using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OfficeAutomation.Models;
using OfficeAutomation.Services.Contracts;
using OfficeAutomation.Services.Security;
using System.Security.Claims;

namespace OfficeAutomation.Controllers;

[Authorize]
public class ContractGuaranteesController : Controller
{
    private readonly IContractGuaranteeService _guaranteeService;
    private readonly IContractService _contractService;
    private readonly ILogger<ContractGuaranteesController> _logger;

    public ContractGuaranteesController(
        IContractGuaranteeService guaranteeService,
        IContractService contractService,
        ILogger<ContractGuaranteesController> logger)
    {
        _guaranteeService = guaranteeService;
        _contractService = contractService;
        _logger = logger;
    }

    private string GetCurrentUserId()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            throw new UnauthorizedAccessException("کاربر احراز هویت نشده است.");
        return userId;
    }

    /// <summary>
    /// Returns the partial view with guarantees for a contract (lazy-loaded in the Payments tab).
    /// </summary>
    [HttpGet("Contracts/{contractId}/Guarantees")]
    public async Task<IActionResult> Index(int contractId, CancellationToken ct)
    {
        // Verify access to the contract
        var userId = GetCurrentUserId();
        await _contractService.EnforceAccessAsync(contractId, userId, ct);

        var guarantees = await _guaranteeService.GetGuaranteesAsync(contractId, ct);
        var canEdit = false;
        try
        {
            var contract = await _contractService.GetContractByIdAsync(contractId, userId, ct);
            canEdit = contract != null && (await _contractService.GetDetailsViewModelAsync(contractId, userId, ct)).CanEdit;
        }
        catch { /* not critical for display */ }

        ViewBag.CanEdit = canEdit;
        ViewBag.ContractId = contractId;
        return PartialView("_Guarantees", guarantees);
    }

    [HttpPost("Contracts/{contractId}/Guarantees/Add")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(int contractId, ContractGuaranteeCreateVM model, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        model.ContractId = contractId;

        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
            return Json(new { success = false, message = string.Join(" | ", errors) });
        }

        try
        {
            var entity = await _guaranteeService.AddGuaranteeAsync(model, userId, ct);
            return Json(new { success = true, message = "ضمانت‌نامه با موفقیت اضافه شد.", id = entity.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding guarantee for contract {ContractId}", contractId);
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpPost("Contracts/{contractId}/Guarantees/Edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int contractId, ContractGuaranteeEditVM model, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        model.ContractId = contractId;

        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
            return Json(new { success = false, message = string.Join(" | ", errors) });
        }

        try
        {
            var entity = await _guaranteeService.UpdateGuaranteeAsync(model, userId, ct);
            return Json(new { success = true, message = "ضمانت‌نامه با موفقیت ویرایش شد." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating guarantee {Id}", model.Id);
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpPost("Contracts/{contractId}/Guarantees/Release/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Release(int contractId, int id, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        try
        {
            var result = await _guaranteeService.ReleaseGuaranteeAsync(id, userId, ct);
            if (!result)
                return Json(new { success = false, message = "ضمانت‌نامه یافت نشد." });

            return Json(new { success = true, message = "ضمانت‌نامه با موفقیت آزاد شد." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpPost("Contracts/{contractId}/Guarantees/Delete/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int contractId, int id, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        try
        {
            var result = await _guaranteeService.DeleteGuaranteeAsync(id, userId, ct);
            if (!result)
                return Json(new { success = false, message = "ضمانت‌نامه یافت نشد." });

            return Json(new { success = true, message = "ضمانت‌نامه با موفقیت حذف شد." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// Returns JSON list of guarantee type options for the dropdown.
    /// </summary>
    [HttpGet("Contracts/Guarantees/Types")]
    public IActionResult GetTypes()
    {
        var types = GuaranteeTypeNames.All.Select(t => new
        {
            value = t,
            text = GuaranteeTypeNames.DisplayNames.TryGetValue(t, out var d) ? d : t
        }).ToList();

        return Json(types);
    }
}