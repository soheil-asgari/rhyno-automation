using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OfficeAutomation.Models;
using OfficeAutomation.Services.Contracts;
using OfficeAutomation.Services.Security;
using System.Security.Claims;

namespace OfficeAutomation.Controllers;

[Authorize]
[PermissionAuthorize("Contract.View")]
public class PaymentPenaltiesController : Controller
{
    private readonly IPaymentPenaltyService _penaltyService;
    private readonly IContractService _contractService;
    private readonly ILogger<PaymentPenaltiesController> _logger;

    public PaymentPenaltiesController(
        IPaymentPenaltyService penaltyService,
        IContractService contractService,
        ILogger<PaymentPenaltiesController> logger)
    {
        _penaltyService = penaltyService;
        _contractService = contractService;
        _logger = logger;
    }

    private string GetCurrentUserId()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            throw new UnauthorizedAccessException("����� ����� ���� ���� ���.");
        return userId;
    }

    /// <summary>
    /// Partial view: Penalty rules and calculated penalties for a contract
    /// </summary>
    [HttpGet("Contracts/{contractId}/Penalties")]
    public async Task<IActionResult> Index(int contractId, CancellationToken ct)
    {
        try
        {
            var userId = GetCurrentUserId();
            var contract = await _contractService.GetContractByIdAsync(contractId, userId, ct);
            if (contract == null) return NotFound();

            // Ensure default rule exists
            await _penaltyService.CreateDefaultRuleAsync(contractId, ct);

            var rules = await _penaltyService.GetRulesForContractAsync(contractId, ct);
            var penalties = await _penaltyService.GetPenaltiesForContractAsync(contractId, ct);

            ViewBag.ContractId = contractId;
            ViewBag.CanEdit = _contractService.CanUserEditContract(contract, userId, User.IsInRole("Admin"));
            ViewBag.PenaltyTypeOptions = PenaltyTypeNames.All
                .Select(t => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem(
                    PenaltyTypeNames.DisplayNames.GetValueOrDefault(t, t), t)).ToList();
            ViewBag.CapTypeOptions = CapTypeNames.All
                .Select(t => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem(
                    CapTypeNames.DisplayNames.GetValueOrDefault(t, t), t)).ToList();

            return PartialView("_Penalties", new PenaltySectionVM { Rules = rules, Penalties = penalties });
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    /// <summary>
    /// Save penalty rule (create or update)
    /// </summary>
    [HttpPost("Contracts/{contractId}/Penalties/SaveRule")]
    [PermissionAuthorize("Contract.Edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveRule(int contractId, PaymentPenaltyRuleEditVM model, int? ruleId, CancellationToken ct)
    {
        try
        {
            var userId = GetCurrentUserId();
            var contract = await _contractService.GetContractByIdAsync(contractId, userId, ct);
            if (contract == null) return NotFound();
            if (!_contractService.CanUserEditContract(contract, userId, User.IsInRole("Admin")))
                return Forbid();

            if (!ModelState.IsValid)
            {
                var errors = string.Join(", ", ModelState.Values
                    .SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                return Json(new { success = false, message = errors });
            }

            model.ContractId = contractId;
            var rule = await _penaltyService.SaveRuleAsync(model, ruleId, ct);
            _logger.LogInformation("Penalty rule saved for contract {ContractId}", contractId);
            return Json(new { success = true, message = "����� ����� ����� ��." });
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving penalty rule for contract {ContractId}", contractId);
            return Json(new { success = false, message = "��� �� ����� ����� �����." });
        }
    }

    /// <summary>
    /// Calculate penalty for a specific payment (manual trigger)
    /// </summary>
    [HttpPost("Contracts/{contractId}/Penalties/Calculate/{paymentId}")]
    [PermissionAuthorize("Contract.Payment")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Calculate(int contractId, int paymentId, CancellationToken ct)
    {
        try
        {
            var userId = GetCurrentUserId();
            var contract = await _contractService.GetContractByIdAsync(contractId, userId, ct);
            if (contract == null) return NotFound();
            if (!_contractService.CanUserViewFinancials(contract, userId, User.IsInRole("Admin")))
                return Forbid();

            var result = await _penaltyService.CalculatePenaltyAsync(paymentId, ct);
            if (result == null)
                return Json(new { success = false, message = "����� ���� ������ ����. ������ ��ј�� ����� �� ����� ����� ���� ���� �����." });

            return Json(new { success = true, message = $"����� {result.CalculatedAmount:N0} ������ ��.", penalty = result });
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating penalty for payment {PaymentId}", paymentId);
            return Json(new { success = false, message = "��� �� ������ �����." });
        }
    }

    /// <summary>
    /// Update penalty status (Waive / Apply)
    /// </summary>
    [HttpPost("Contracts/{contractId}/Penalties/UpdateStatus/{penaltyId}")]
    [PermissionAuthorize("Contract.Edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(int contractId, int penaltyId, string status, CancellationToken ct)
    {
        try
        {
            var userId = GetCurrentUserId();
            var contract = await _contractService.GetContractByIdAsync(contractId, userId, ct);
            if (contract == null) return NotFound();
            if (!_contractService.CanUserEditContract(contract, userId, User.IsInRole("Admin")))
                return Forbid();

            var ok = await _penaltyService.UpdatePenaltyStatusAsync(penaltyId, status, ct);
            if (!ok)
                return Json(new { success = false, message = "����� ���� ���." });

            return Json(new { success = true, message = status == "Waived" ? "����� ������ ��." : "����� ����� ����� ��." });
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating penalty status {PenaltyId}", penaltyId);
            return Json(new { success = false, message = "��� �� ���������� �����." });
        }
    }

    /// <summary>
    /// Get penalty type options for dropdown
    /// </summary>
    [HttpGet("Contracts/Penalties/Types")]
    public IActionResult GetTypes()
    {
        var types = PenaltyTypeNames.All
            .Select(t => new { value = t, text = PenaltyTypeNames.DisplayNames.GetValueOrDefault(t, t) });
        return Json(types);
    }
}
