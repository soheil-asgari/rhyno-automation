using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using OfficeAutomation.Models;
using OfficeAutomation.Services.Contracts;
using OfficeAutomation.Services.Security;
using System.Security.Claims;

namespace OfficeAutomation.Controllers;

[Authorize]
[PermissionAuthorize("Contract.View")]
public class ContractsController : Controller
{
    private readonly IContractService _contractService;
    private readonly ISignatureService _signatureService;
    private readonly IAttachmentVersionService _attachmentVersionService;
    private readonly IContractFieldValidator _fieldValidator;
    private readonly ILogger<ContractsController> _logger;
private readonly IContractReportService _reportService;
private readonly IContractExportService _exportService;

    public ContractsController(IContractService contractService, ISignatureService signatureService, IAttachmentVersionService attachmentVersionService, IContractFieldValidator fieldValidator, IContractReportService reportService, IContractExportService exportService, ILogger<ContractsController> logger)
    {
        _contractService = contractService;
        _signatureService = signatureService;
        _attachmentVersionService = attachmentVersionService;
        _fieldValidator = fieldValidator;
        _reportService = reportService;
        _exportService = exportService;
        _logger = logger;
    }

    private string GetCurrentUserId()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            throw new UnauthorizedAccessException("ع©ط§ط±ط¨ط± ط§ط­ط±ط§ط² ظ‡ظˆغŒطھ ظ†ط´ط¯ظ‡ ط§ط³طھ.");
        }
        return userId;
    }

    /// <summary>
    /// Converts a Persian (Shamsi) date string (e.g. 1403/05/29) to Gregorian DateTime.
    /// Returns null for invalid, empty, or unparseable input.
    /// </summary>
    public static DateTime? ToGregorianDate(string? persianDateStr)
    {
        if (string.IsNullOrWhiteSpace(persianDateStr)) return null;
        try
        {
            var parts = persianDateStr.Split('/');
            if (parts.Length != 3) return null;

            // Convert Persian digits to English digits
            string yearStr = string.Concat(parts[0].Select(c => char.GetNumericValue(c) >= 0 ? char.GetNumericValue(c).ToString() : c.ToString()));
            string monthStr = string.Concat(parts[1].Select(c => char.GetNumericValue(c) >= 0 ? char.GetNumericValue(c).ToString() : c.ToString()));
            string dayStr = string.Concat(parts[2].Select(c => char.GetNumericValue(c) >= 0 ? char.GetNumericValue(c).ToString() : c.ToString()));

            int year = int.Parse(yearStr);
            int month = int.Parse(monthStr);
            int day = int.Parse(dayStr);

            var pc = new System.Globalization.PersianCalendar();
            return pc.ToDateTime(year, month, day, 0, 0, 0, 0);
        }
        catch
        {
            return null;
        }
    }

    // â•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گ
    //  Index
    // â•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گ

    [HttpGet]
    public async Task<IActionResult> Index(ContractIndexVM filter, CancellationToken ct)
    {
        var currentUserId = GetCurrentUserId();
        var vm = await _contractService.GetIndexViewModelAsync(filter, currentUserId, ct);
        return View(vm);
    }

    // â•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گ
    //  Dashboard
    // â•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گ

    [HttpGet("Contracts/Dashboard")]
    public async Task<IActionResult> Dashboard(CancellationToken ct)
    {
        var currentUserId = GetCurrentUserId();
        var vm = await _contractService.GetDashboardAsync(currentUserId, ct);
        return View(vm);
    }

// ═══════════════════════════════════════════════════════════════
    //  Reports
    // ═══════════════════════════════════════════════════════════════

    [HttpGet("Contracts/Reports")]
    [PermissionAuthorize("Contract.View")]
    public async Task<IActionResult> Reports(int? obligationsMonths, CancellationToken ct)
    {
        var vm = await _reportService.GetAllReportsAsync(obligationsMonths ?? 6, ct);
        return View(vm);
    }

    [HttpGet("Contracts/Reports/Data/Aging")]
    public async Task<IActionResult> AgingReportData(CancellationToken ct)
    {
        var vm = await _reportService.GetPaymentAgingReportAsync(ct);
        return Json(vm);
    }

    [HttpGet("Contracts/Reports/Data/Revenue")]
    public async Task<IActionResult> RevenueReportData(CancellationToken ct)
    {
        var vm = await _reportService.GetRevenueRecognitionScheduleAsync(ct);
        return Json(vm);
    }

    [HttpGet("Contracts/Reports/Data/Pipeline")]
    public async Task<IActionResult> PipelineReportData(CancellationToken ct)
    {
        var vm = await _reportService.GetContractValuePipelineAsync(ct);
        return Json(vm);
    }

    [HttpGet("Contracts/Reports/Data/Calendar")]
    public async Task<IActionResult> CalendarReportData(int? months, CancellationToken ct)
    {
        var vm = await _reportService.GetObligationsCalendarAsync(months ?? 6, ct);
        return Json(vm);
    }

    [HttpGet("Contracts/Reports/Data/Exposure")]
    public async Task<IActionResult> ExposureReportData(CancellationToken ct)
    {
        var vm = await _reportService.GetFinancialExposureAsync(ct);
        return Json(vm);
    }
    // â•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گ
    //  Create
    // â•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گ

    [HttpGet]
    [PermissionAuthorize("Contract.Create")]
    public async Task<IActionResult> Create(CancellationToken ct)
    {
        var currentUserId = GetCurrentUserId();
        var vm = await _contractService.BuildCreateViewModelAsync(currentUserId, ct);
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize("Contract.Create")]
    public async Task<IActionResult> Create(ContractCreateVM model, CancellationToken ct)
    {
        // Safely convert Persian date display strings to Gregorian if the hidden DateTime fields are MinValue / uninitialized
        if (model.StartDate == default || model.StartDate.Year < 1700)
        {
            var converted = ToGregorianDate(model.StartDateDisplay);
            if (converted.HasValue) model.StartDate = converted.Value;
        }
        if ((!model.EndDate.HasValue || model.EndDate.Value.Year < 1700) && !string.IsNullOrWhiteSpace(model.EndDateDisplay))
        {
            var converted = ToGregorianDate(model.EndDateDisplay);
            if (converted.HasValue) model.EndDate = converted.Value;
        }
        if ((!model.SignDate.HasValue || model.SignDate.Value.Year < 1700) && !string.IsNullOrWhiteSpace(model.SignDateDisplay))
        {
            var converted = ToGregorianDate(model.SignDateDisplay);
            if (converted.HasValue) model.SignDate = converted.Value;
        }

        // ── Dynamic field validation ──
        var fieldErrors = _fieldValidator.ValidateFields(model.ContractType ?? string.Empty, model.TypeFields, model.FieldValues);
        foreach (var err in fieldErrors)
        {
            ModelState.AddModelError($"FieldValues[{err.Key}]", err.Value);
        }

        if (!ModelState.IsValid)
        {
            var currentUserId = GetCurrentUserId();
            var vm = await _contractService.BuildCreateViewModelAsync(currentUserId, ct);
            vm.Title = model.Title; vm.ContractNumber = model.ContractNumber; vm.ClientName = model.ClientName;
            vm.ClientPhone = model.ClientPhone; vm.ClientAddress = model.ClientAddress; vm.Description = model.Description;
            vm.Notes = model.Notes;
            vm.TermsAndConditions = model.TermsAndConditions; vm.ContractBodyHtml = model.ContractBodyHtml;
            vm.TotalAmount = model.TotalAmount; vm.StartDate = model.StartDate; vm.EndDate = model.EndDate;
            vm.SignDate = model.SignDate; vm.ExpiryNoticeDays = model.ExpiryNoticeDays;
            vm.Status = model.Status; vm.AssignedUserId = model.AssignedUserId; vm.TemplateId = model.TemplateId;
            vm.CustomerId = model.CustomerId; vm.ContractType = model.ContractType;
            vm.FieldValues = model.FieldValues;
            vm.TypeFields = model.TypeFields;
            return View(vm);
        }
        try
        {
            var currentUser = GetCurrentUserId();
            var contract = await _contractService.CreateContractAsync(model, currentUser, ct);
            TempData["SuccessMessage"] = "ظ‚ط±ط§ط±ط¯ط§ط¯ ط¨ط§ ظ…ظˆظپظ‚غŒطھ ط§غŒط¬ط§ط¯ ط´ط¯.";
            return RedirectToAction(nameof(Details), new { id = contract.Id });
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError("ContractNumber", ex.Message);
            var currentUser = GetCurrentUserId();
            var vm = await _contractService.BuildCreateViewModelAsync(currentUser, ct);
            vm.Title = model.Title; vm.ContractNumber = model.ContractNumber; vm.ClientName = model.ClientName;
            vm.ClientPhone = model.ClientPhone; vm.ClientAddress = model.ClientAddress; vm.Description = model.Description;
            vm.Notes = model.Notes;
            vm.TermsAndConditions = model.TermsAndConditions; vm.ContractBodyHtml = model.ContractBodyHtml;
            vm.TotalAmount = model.TotalAmount; vm.StartDate = model.StartDate; vm.EndDate = model.EndDate;
            vm.SignDate = model.SignDate; vm.ExpiryNoticeDays = model.ExpiryNoticeDays;
            vm.Status = model.Status; vm.AssignedUserId = model.AssignedUserId; vm.TemplateId = model.TemplateId;
            vm.CustomerId = model.CustomerId; vm.ContractType = model.ContractType;
            vm.FieldValues = model.FieldValues;
            vm.TypeFields = model.TypeFields;
            return View(vm);
        }
    }

    // â•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گ
    //  Edit
    // â•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گ

    [HttpGet]
    [PermissionAuthorize("Contract.Edit")]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        try
        {
            // Enforce RLS - will throw if user cannot access
            var currentUserId = GetCurrentUserId();
            await _contractService.EnforceAccessAsync(id, currentUserId, ct);
            var vm = await _contractService.BuildEditViewModelAsync(id, currentUserId, ct);
            return View(vm);
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize("Contract.Edit")]
    public async Task<IActionResult> Edit(ContractEditVM model, CancellationToken ct)
    {
        // ── Dynamic field validation ──
        var fieldErrors = _fieldValidator.ValidateFields(model.ContractType ?? string.Empty, model.TypeFields, model.FieldValues);
        foreach (var err in fieldErrors)
        {
            ModelState.AddModelError($"FieldValues[{err.Key}]", err.Value);
        }

        if (!ModelState.IsValid)
        {
            var currentUserId = GetCurrentUserId();
            try
            {
                var vm = await _contractService.BuildEditViewModelAsync(model.Id, currentUserId, ct);
                vm.Title = model.Title; vm.ContractNumber = model.ContractNumber; vm.ClientName = model.ClientName;
                vm.ClientPhone = model.ClientPhone; vm.ClientAddress = model.ClientAddress; vm.Description = model.Description;
                vm.Notes = model.Notes;
                vm.TermsAndConditions = model.TermsAndConditions; vm.ContractBodyHtml = model.ContractBodyHtml;
                vm.TotalAmount = model.TotalAmount; vm.PaidAmount = model.PaidAmount;
                vm.StartDate = model.StartDate; vm.EndDate = model.EndDate; vm.Status = model.Status;
                vm.SignDate = model.SignDate; vm.ExpiryNoticeDays = model.ExpiryNoticeDays;
                vm.AssignedUserId = model.AssignedUserId; vm.TemplateId = model.TemplateId;
                vm.CustomerId = model.CustomerId; vm.ContractType = model.ContractType;
                vm.FieldValues = model.FieldValues;
                vm.TypeFields = model.TypeFields;
                return View(vm);
            }
            catch (KeyNotFoundException) { return NotFound(); }
        }
        try
        {
            var currentUserId = GetCurrentUserId();
            await _contractService.EnforceAccessAsync(model.Id, currentUserId, ct);
            await _contractService.UpdateContractAsync(model, currentUserId, ct);
            TempData["SuccessMessage"] = "ظ‚ط±ط§ط±ط¯ط§ط¯ ط¨ط§ ظ…ظˆظپظ‚غŒطھ ظˆغŒط±ط§غŒط´ ط´ط¯.";
            return RedirectToAction(nameof(Details), new { id = model.Id });
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError("ContractNumber", ex.Message);
            var currentUserId = GetCurrentUserId();
            var vm = await _contractService.BuildEditViewModelAsync(model.Id, currentUserId, ct);
            vm.FieldValues = model.FieldValues;
            vm.TypeFields = model.TypeFields;
            return View(vm);
        }
    }

    // â•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گ
    //  Details
    // â•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گ

    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            var vm = await _contractService.GetDetailsViewModelAsync(id, currentUserId, ct);
            return View(vm);
        }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    // â•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گ
    //  Delete
    // â•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گ

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize("Contract.Delete")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var currentUserId = GetCurrentUserId();
        try
        {
            await _contractService.EnforceAccessAsync(id, currentUserId, ct);
        }
        catch (UnauthorizedAccessException) { return Forbid(); }

        var deleted = await _contractService.DeleteContractAsync(id, currentUserId, ct);
        if (!deleted) return NotFound();
        TempData["SuccessMessage"] = "ظ‚ط±ط§ط±ط¯ط§ط¯ ط¨ط§ ظ…ظˆظپظ‚غŒطھ ط­ط°ظپ ط´ط¯.";
        return RedirectToAction(nameof(Index));
    }

    // â•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گ
    //  Archive
    // â•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گ

    [HttpGet]
    [PermissionAuthorize("Contract.Archive")]
    public async Task<IActionResult> Archive(ContractArchiveVM filter, CancellationToken ct)
    {
        var currentUserId = GetCurrentUserId();
        var vm = await _contractService.GetArchiveViewModelAsync(filter, currentUserId, ct);
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize("Contract.Archive")]
    public async Task<IActionResult> ArchiveContract(int id, CancellationToken ct)
    {
        var currentUserId = GetCurrentUserId();
        try
        {
            await _contractService.EnforceAccessAsync(id, currentUserId, ct);
        }
        catch (UnauthorizedAccessException) { return Forbid(); }

        var archived = await _contractService.ArchiveContractAsync(id, currentUserId, ct);
        if (!archived) return NotFound();
        TempData["SuccessMessage"] = "ظ‚ط±ط§ط±ط¯ط§ط¯ ط¨ط§ ظ…ظˆظپظ‚غŒطھ ط¨ط§غŒع¯ط§ظ†غŒ ط´ط¯.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize("Contract.Archive")]
    public async Task<IActionResult> UnarchiveContract(int id, CancellationToken ct)
    {
        var currentUserId = GetCurrentUserId();
        try
        {
            await _contractService.EnforceAccessAsync(id, currentUserId, ct);
        }
        catch (UnauthorizedAccessException) { return Forbid(); }

        var unarchived = await _contractService.UnarchiveContractAsync(id, currentUserId, ct);
        if (!unarchived) return NotFound();
        TempData["SuccessMessage"] = "ظ‚ط±ط§ط±ط¯ط§ط¯ ط§ط² ط¨ط§غŒع¯ط§ظ†غŒ ط®ط§ط±ط¬ ط´ط¯.";
        return RedirectToAction(nameof(Details), new { id });
    }

    // â•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گ
    //  Payments
    // â•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گ

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize("Contract.Payment")]
    public async Task<IActionResult> AddPayment(ContractPaymentCreateVM model, CancellationToken ct)
    {
        // If dates arrived as Persian date strings (from hidden fields that bypass model binding),
        // parse them to Gregorian DateTime here as a safety net.
        // The front-end JS converts before submit, but this guards against direct posts.
        if (ModelState.IsValid == false)
        {
            // Attempt to fix Persian-date strings that couldn't bind to DateTime
            var paymentDateRaw = HttpContext.Request.Form["PaymentDate"].FirstOrDefault();
            var dueDateRaw = HttpContext.Request.Form["DueDate"].FirstOrDefault();

            if (paymentDateRaw != null && model.PaymentDate == default)
            {
                var parsed = ToGregorianDate(paymentDateRaw);
                if (parsed.HasValue)
                {
                    model.PaymentDate = parsed.Value;
                    ModelState.Remove("PaymentDate");
                }
            }
            if (dueDateRaw != null && model.DueDate == null)
            {
                var parsed = ToGregorianDate(dueDateRaw);
                if (parsed.HasValue)
                {
                    model.DueDate = parsed.Value;
                    ModelState.Remove("DueDate");
                }
            }

            // Re-validate after patching
            if (!ModelState.IsValid)
            {
                var errors = string.Join(" | ", ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage));
                _logger.LogWarning("AddPayment ModelState invalid: {Errors}", errors);
                TempData["ErrorMessage"] = "ط§ط·ظ„ط§ط¹ط§طھ ظ¾ط±ط¯ط§ط®طھ ظ…ط¹طھط¨ط± ظ†غŒط³طھ.";
                return RedirectToAction(nameof(Details), new { id = model.ContractId });
            }
        }

        try
        {
            var currentUser = GetCurrentUserId();
            await _contractService.AddPaymentAsync(model, currentUser, ct);
            TempData["SuccessMessage"] = "ظ¾ط±ط¯ط§ط®طھ ط¨ط§ ظ…ظˆظپظ‚غŒطھ ط«ط¨طھ ط´ط¯.";
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        return RedirectToAction(nameof(Details), new { id = model.ContractId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize("Contract.Payment")]
    public async Task<IActionResult> DeletePayment(int paymentId, int contractId, CancellationToken ct)
    {
        try
        {
            var currentUser = GetCurrentUserId();
            var deleted = await _contractService.DeletePaymentAsync(paymentId, contractId, currentUser, ct);
            if (!deleted) return NotFound();
            TempData["SuccessMessage"] = "ظ¾ط±ط¯ط§ط®طھ ط¨ط§ ظ…ظˆظپظ‚غŒطھ ط­ط°ظپ ط´ط¯.";
            return RedirectToAction(nameof(Details), new { id = contractId });
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    // â•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گ
    //  Attachments
    // â•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گ

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize("Contract.Edit")]
    public async Task<IActionResult> UploadAttachment(int contractId, IFormFile file, CancellationToken ct)
    {
        if (file == null || file.Length == 0)
        {
            TempData["ErrorMessage"] = "ظپط§غŒظ„ ط§ظ†طھط®ط§ط¨ ظ†ط´ط¯ظ‡ ط§ط³طھ.";
            return RedirectToAction(nameof(Details), new { id = contractId });
        }

        try
        {
            var currentUserId = GetCurrentUserId();
            await using var stream = file.OpenReadStream();
            await _contractService.UploadAttachmentAsync(contractId, file.FileName, stream, file.ContentType, currentUserId, ct);
            TempData["SuccessMessage"] = "ظپط§غŒظ„ ط¨ط§ ظ…ظˆظپظ‚غŒطھ ط¨ط§ط±ع¯ط°ط§ط±غŒ ط´ط¯.";
        }
        catch (KeyNotFoundException) { return NotFound(); }
        return RedirectToAction(nameof(Details), new { id = contractId });
    }

    [HttpGet("Contracts/DownloadAttachment/{attachmentId}")]
    public async Task<IActionResult> DownloadAttachment(int attachmentId, CancellationToken ct)
    {
        var currentUser = GetCurrentUserId();
        var (attachment, fullPath) = await _contractService.GetAttachmentForDownloadAsync(attachmentId, currentUser, ct);
        if (attachment == null || fullPath == null) return NotFound();
        if (!System.IO.File.Exists(fullPath)) return NotFound();

        return PhysicalFile(fullPath, attachment.ContentType ?? "application/octet-stream", attachment.FileName);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize("Contract.Edit")]
    public async Task<IActionResult> DeleteAttachment(int attachmentId, int contractId, CancellationToken ct)
    {
        try
        {
            var currentUser = GetCurrentUserId();
            var deleted = await _contractService.DeleteAttachmentAsync(attachmentId, contractId, currentUser, ct);
            if (!deleted) return NotFound();
            TempData["SuccessMessage"] = "ظپط§غŒظ„ ظ¾غŒظˆط³طھ ط­ط°ظپ ط´ط¯.";
            return RedirectToAction(nameof(Details), new { id = contractId });
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    // â•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گ
    //  Document Generation
    // â•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گ

    [HttpGet("Contracts/GenerateDocument/{id}")]
    [PermissionAuthorize("Contract.View")]
    public async Task<IActionResult> GenerateDocument(int id, CancellationToken ct)
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            var vm = await _contractService.GetDetailsViewModelAsync(id, currentUserId, ct);
            ViewData["Title"] = "ع†ط§ظ¾ / PDF ظ‚ط±ط§ط±ط¯ط§ط¯";
            return View("GenerateDocument", vm);
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    // â•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گ
    //  Settings / Templates
    // â•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گ

    [HttpGet]
    [PermissionAuthorize("Contract.Settings")]
    public async Task<IActionResult> Settings(CancellationToken ct)
    {
        var currentUserId = GetCurrentUserId();
        var vm = await _contractService.GetSettingsViewModelAsync(currentUserId, ct);
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize("Contract.Settings")]
    public async Task<IActionResult> Settings(ContractSettingsVM model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            var currentUserId = GetCurrentUserId();
            var vm = await _contractService.GetSettingsViewModelAsync(currentUserId, ct);
            vm.DefaultStatus = model.DefaultStatus;
            vm.DefaultResponsibleUserId = model.DefaultResponsibleUserId;
            vm.NotifyOnCreate = model.NotifyOnCreate;
            return View(vm);
        }

        var currentUser = GetCurrentUserId();
        model.UserId = currentUser;
        await _contractService.SaveSettingsAsync(model, currentUser, ct);
        TempData["SuccessMessage"] = "طھظ†ط¸غŒظ…ط§طھ ط¨ط§ ظ…ظˆظپظ‚غŒطھ ط°ط®غŒط±ظ‡ ط´ط¯.";
        return RedirectToAction(nameof(Settings));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize("Contract.Settings")]
    public async Task<IActionResult> SaveTemplate(ContractSettingsVM model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            var currentUserId = GetCurrentUserId();
            var vm = await _contractService.GetSettingsViewModelAsync(currentUserId, ct);
            vm.TemplateName = model.TemplateName; vm.HtmlContent = model.HtmlContent;
            vm.IsDefault = model.IsDefault;
            return View(nameof(Settings), vm);
        }
        var currentUser = GetCurrentUserId();
        await _contractService.SaveTemplateAsync(model, currentUser, ct);
        TempData["SuccessMessage"] = "ظ‚ط§ظ„ط¨ ط¨ط§ ظ…ظˆظپظ‚غŒطھ ط°ط®غŒط±ظ‡ ط´ط¯.";
        return RedirectToAction(nameof(Settings));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize("Contract.Settings")]
    public async Task<IActionResult> DeleteTemplate(int templateId, CancellationToken ct)
    {
        var currentUserId = GetCurrentUserId();
        var deleted = await _contractService.DeleteTemplateAsync(templateId, currentUserId, ct);
        if (!deleted) return NotFound();
        TempData["SuccessMessage"] = "ظ‚ط§ظ„ط¨ ط¨ط§ ظ…ظˆظپظ‚غŒطھ ط­ط°ظپ ط´ط¯.";
        return RedirectToAction(nameof(Settings));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize("Contract.Settings")]
    public async Task<IActionResult> SetDefaultTemplate(int templateId, CancellationToken ct)
    {
        var currentUserId = GetCurrentUserId();
        var set = await _contractService.SetDefaultTemplateAsync(templateId, currentUserId, ct);
        if (!set) return NotFound();
        TempData["SuccessMessage"] = "ظ‚ط§ظ„ط¨ ظ¾غŒط´â€Œظپط±ط¶ ط¨ط§ ظ…ظˆظپظ‚غŒطھ طھظ†ط¸غŒظ… ط´ط¯.";
        return RedirectToAction(nameof(Settings));
    }

    // â•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گ
    //  Template Preview (for auto-populate in Create/Edit)
    // â•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گ

    [HttpGet("Contracts/PreviewTemplate")]
    [PermissionAuthorize("Contract.View")]
    public async Task<IActionResult> PreviewTemplate(int id, bool raw = false, CancellationToken ct = default)
    {
        var currentUserId = GetCurrentUserId();
        var template = await _contractService.GetUserTemplatesAsync(currentUserId, ct);
        var t = template.FirstOrDefault(x => x.Id == id);
        if (t == null) return NotFound();
        if (raw)
        {
            return Content(t.HtmlContent ?? "", "text/plain", System.Text.Encoding.UTF8);
        }
        var preview = await _contractService.PreviewTemplateAsync(id, currentUserId, ct);
        return Content(preview, "text/html", System.Text.Encoding.UTF8);
    }
// â•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گ
    //  E-Signature: Internal Sign
    // â•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گ

    [HttpPost("Contracts/{contractId}/Sign")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SignContract(int contractId, [FromBody] SignContractRequest request, CancellationToken ct)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.SignatureImageData))
            return Json(new { success = false, message = "ط¯ط§ط¯ظ‡â€Œظ‡ط§غŒ ط§ظ…ط¶ط§ ط®ط§ظ„غŒ ط§ط³طھ." });

        try
        {
            var userId = GetCurrentUserId();
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "0.0.0.0";
            var userAgent = Request.Headers.UserAgent.ToString();

            var signer = await _signatureService.CaptureInternalSignatureAsync(
                contractId, request.SignatureImageData, request.Comment, ipAddress, userAgent, userId, ct);

            return Json(new { success = true, message = "ط§ظ…ط¶ط§ ط¨ط§ ظ…ظˆظپظ‚غŒطھ ط«ط¨طھ ط´ط¯.", signerId = signer.Id, signedAt = signer.SignedAt });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sign contract {ContractId}", contractId);
            return Json(new { success = false, message = $"ط®ط·ط§ ط¯ط± ط«ط¨طھ ط§ظ…ط¶ط§: {ex.Message}" });
        }
    }

    // â•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گ
    //  E-Signature: Get External Sign Link
    // â•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گ

    [HttpPost("Contracts/{contractId}/GetExternalSignLink")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GetExternalSignLink(int contractId, [FromBody] GetExternalSignLinkRequest request, CancellationToken ct)
    {
        if (request == null || request.SignerId <= 0)
            return Json(new { success = false, message = "ط´ظ†ط§ط³ظ‡ ط§ظ…ط¶ط§ع©ظ†ظ†ط¯ظ‡ ظ†ط§ظ…ط¹طھط¨ط± ط§ط³طھ." });

        try
        {
            var link = await _signatureService.GenerateExternalSignerLinkAsync(contractId, request.SignerId, ct);
            return Json(new { success = true, link });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate external sign link for contract {ContractId}", contractId);
            return Json(new { success = false, message = $"ط®ط·ط§ ط¯ط± طھظˆظ„غŒط¯ ظ„غŒظ†ع©: {ex.Message}" });
        }
    }

    // â•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گ
    //  E-Signature: Create Signature Request
    // â•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گ

    [HttpPost("Contracts/Signature/CreateRequest/{contractId}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateSignatureRequest(int contractId, [FromBody] CreateSignatureRequestVm model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return Json(new { success = false, message = "ط§ط·ظ„ط§ط¹ط§طھ ظ†ط§ظ…ط¹طھط¨ط± ط§ط³طھ." });

        try
        {
            var userId = GetCurrentUserId();
            var result = await _signatureService.CreateSignatureRequestAsync(
                contractId, model.Title, model.Message, model.DeliveryMethod,
                model.SigningOrder, model.AutoActivateContract, model.AmendmentId, userId, ct);
            return Json(new { success = true, requestId = result.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create signature request for contract {ContractId}", contractId);
            return Json(new { success = false, message = $"ط®ط·ط§ ط¯ط± ط§غŒط¬ط§ط¯ ط¯ط±ط®ظˆط§ط³طھ: {ex.Message}" });
        }
    }

    // â•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گ
    //  E-Signature: Get Signature Requests
    // â•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گâ•گ

    [HttpGet("Contracts/{contractId}/SignatureRequests")]
    public async Task<IActionResult> GetSignatureRequests(int contractId, CancellationToken ct)
    {
        try
        {
            var requests = await _signatureService.GetSignatureRequestsAsync(contractId, ct);
            var data = requests.Select(r => new
            {
                r.Id, r.Title, r.Status, r.DeliveryMethod, r.SigningOrder, r.AutoActivateContract, r.CreatedAt, r.CompletedAt,
                Signers = r.Signers.Select(s => new
                {
                    s.Id, s.UserId, UserName = s.User?.FullName ?? s.FullName ?? s.Email ?? "ظ†ط§ظ…ط´ط®طµ",
                    s.SignerType, s.Result, s.SignOrder, s.SignedAt, hasSignatureImage = s.SignatureImageData != null
                }),
                AuditLogs = r.AuditLogs.Select(a => new { a.EventType, a.Description, a.CreatedAt })
            });
            return Json(new { success = true, data });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get signature requests for contract {ContractId}", contractId);
            return Json(new { success = false, message = ex.Message });
        }
    }
// ════════════════════════════════════════════════════════════
    //  Attachment Versioning
    // ════════════════════════════════════════════════════════════

    [HttpGet("Contracts/Attachment/{attachmentId}/Versions")]
    public async Task<IActionResult> GetAttachmentVersions(int attachmentId, CancellationToken ct)
    {
        try
        {
            var versions = await _attachmentVersionService.GetVersionsAsync(attachmentId, ct);
            return Json(new { success = true, data = versions });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get versions for attachment {AttachmentId}", attachmentId);
            return Json(new { success = false, message = "Ø®Ø·Ø§ Ø¯Ø± Ø¨Ø§Ø²ÛŒØ§Ø¨ÛŒ Ù†Ø³Ø®Ù‡â€ŒÙ‡Ø§." });
        }
    }

    [HttpPost("Contracts/Attachment/{attachmentId}/UploadVersion")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadAttachmentVersion(int attachmentId, IFormFile file, string? changeDescription, CancellationToken ct)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (file == null || file.Length == 0)
                return Json(new { success = false, message = "ÙØ§ÛŒÙ„ÛŒ Ø§Ù†ØªØ®Ø§Ø¨ Ù†Ø´Ø¯Ù‡ Ø§Ø³Øª." });

            await using var stream = file.OpenReadStream();
            var version = await _attachmentVersionService.UploadNewVersionAsync(
                attachmentId, file.FileName, file.ContentType ?? "application/octet-stream",
                file.Length, stream, userId, changeDescription, ct);

            return Json(new { success = true, versionId = version.Id, versionNumber = version.VersionNumber });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload new version for attachment {AttachmentId}", attachmentId);
            return Json(new { success = false, message = "Ø®Ø·Ø§ Ø¯Ø± Ø¨Ø§Ø±Ú¯Ø°Ø§Ø±ÛŒ Ù†Ø³Ø®Ù‡ Ø¬Ø¯ÛŒØ¯: " + ex.Message });
        }
    }

    [HttpGet("Contracts/Attachment/Version/{versionId}/Download")]
    public async Task<IActionResult> DownloadAttachmentVersion(int versionId, CancellationToken ct)
    {
        try
        {
            var userId = GetCurrentUserId();
            var filePath = await _attachmentVersionService.GetVersionFilePathAsync(versionId, userId, ct);
            if (filePath == null)
                return NotFound();

            return PhysicalFile(filePath, "application/octet-stream", Path.GetFileName(filePath));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download version {VersionId}", versionId);
            return NotFound();
        }
    }

    [HttpPost("Contracts/Attachment/Version/{versionId}/Restore")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RestoreAttachmentVersion(int versionId, CancellationToken ct)
    {
        try
        {
            var userId = GetCurrentUserId();
            var version = await _attachmentVersionService.RestoreVersionAsync(versionId, userId, ct);
            return Json(new { success = true, versionId = version.Id, versionNumber = version.VersionNumber });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restore version {VersionId}", versionId);
            return Json(new { success = false, message = "Ø®Ø·Ø§ Ø¯Ø± Ø¨Ø§Ø²Ú¯Ø±Ø¯Ø§Ù†ÛŒ Ù†Ø³Ø®Ù‡: " + ex.Message });
        }
    }
// ════════════════════════════════════════════════════════════
    //  Attachment Preview
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Returns file content for previewing an attachment in the browser.
    /// For PDF/images/text, returns the file stream directly.
    /// For unsupported types, returns a 404 with an error message.
    /// </summary>
    [HttpGet("Contracts/Attachment/{attachmentId}/Preview")]
    public async Task<IActionResult> PreviewAttachment(int attachmentId, CancellationToken ct)
    {
        try
        {
            var userId = GetCurrentUserId();
            // Get attachment with access check
            var attachment = await _contractService.GetAttachmentAsync(attachmentId, userId, ct);
            if (attachment == null)
                return NotFound();

            // Determine the file path from the current version
            string? filePath = null;
            string? contentType = attachment.ContentType;
            string fileName = attachment.FileName;

            if (attachment.CurrentVersionId.HasValue)
            {
                filePath = await _attachmentVersionService.GetVersionFilePathAsync(attachment.CurrentVersionId.Value, userId, ct);
            }

            if (filePath == null || !System.IO.File.Exists(filePath))
                return NotFound();

            var ext = System.IO.Path.GetExtension(fileName).ToLowerInvariant();
            var previewableImageExts = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff", ".tif", ".webp", ".svg" };
            var previewableTextExts = new[] { ".txt", ".csv", ".log", ".json", ".xml", ".md", ".yaml", ".yml", ".ini", ".cfg", ".config" };

            // PDF: return file stream
            if (ext == ".pdf")
            {
                return PhysicalFile(filePath, contentType ?? "application/pdf", fileName);
            }

            // Images: return file stream
            if (previewableImageExts.Contains(ext))
            {
                var imageContentType = contentType;
                if (string.IsNullOrEmpty(imageContentType) || imageContentType == "application/octet-stream")
                {
                    imageContentType = ext switch
                    {
                        ".jpg" or ".jpeg" => "image/jpeg",
                        ".png" => "image/png",
                        ".gif" => "image/gif",
                        ".bmp" => "image/bmp",
                        ".tiff" or ".tif" => "image/tiff",
                        ".webp" => "image/webp",
                        ".svg" => "image/svg+xml",
                        _ => "application/octet-stream"
                    };
                }
                return PhysicalFile(filePath, imageContentType, fileName);
            }

            // Text: read content and return as plain text
            if (previewableTextExts.Contains(ext))
            {
                var text = await System.IO.File.ReadAllTextAsync(filePath, ct);
                // Limit to first 500KB to avoid huge responses
                if (text.Length > 512000)
                    text = text[..512000] + "\n\n... (فایل بزرگتر از حد مجاز برای نمایش است)";
                return Content(text, "text/plain; charset=utf-8");
            }

            // Unsupported type: return JSON with preview-not-available indicator
            return Ok(new
            {
                success = false,
                previewUnavailable = true,
                message = "پیش‌نمایش برای این نوع فایل در دسترس نیست.",
                fileName,
                fileSize = attachment.FileSize,
                contentType = contentType
            });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to preview attachment {AttachmentId}", attachmentId);
            return NotFound();
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  Bulk Action
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Executes a bulk operation on multiple contracts (status change, assign,
    /// archive, delete). Returns a JSON result for frontend consumption.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BulkAction([FromBody] BulkActionRequest request, CancellationToken ct)
    {
        if (request == null || request.IDs == null || request.IDs.Count == 0)
        {
            return Ok(new BulkActionResult
            {
                FailureCount = 1,
                Message = "هیچ قراردادی انتخاب نشده است."
            });
        }

        if (string.IsNullOrWhiteSpace(request.Action))
        {
            return Ok(new BulkActionResult
            {
                FailureCount = 1,
                Message = "عملیات مشخص نشده است."
            });
        }

        try
        {
            var currentUserId = GetCurrentUserId();
            var result = await _contractService.BulkActionAsync(request, currentUserId, ct);
            return Ok(result);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Bulk action failed");
            return Ok(new BulkActionResult
            {
                FailureCount = request.IDs.Count,
                Message = "خطا در انجام عملیات گروهی."
            });
        }
    }

    // ════════════════════════════════════════════════════════════
    //  Export: Contract List → Excel
    // ════════════════════════════════════════════════════════════

    [HttpGet("Contracts/Export/Contracts")]
    public async Task<IActionResult> ExportContracts([FromQuery] ContractIndexVM filters, [FromQuery] string format = "excel", CancellationToken ct = default)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (format.Equals("excel", StringComparison.OrdinalIgnoreCase))
            {
                var bytes = await _exportService.ExportContractsToExcelAsync(filters, userId, ct);
                return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Contracts_{DateTime.Now:yyyyMMdd}.xlsx");
            }
            return BadRequest(new { message = "Unsupported format. Use 'excel'." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export contracts");
            return BadRequest(new { message = "Export failed." });
        }
    }

    // ════════════════════════════════════════════════════════════
    //  Export: Reports → Excel / PDF
    // ════════════════════════════════════════════════════════════

    [HttpGet("Contracts/Reports/Export/{reportType}")]
    public async Task<IActionResult> ExportReport(string reportType, [FromQuery] string format = "excel", CancellationToken ct = default)
    {
        try
        {
            var validTypes = new[] { "aging", "revenue", "pipeline", "calendar", "exposure" };
            if (!validTypes.Contains(reportType.ToLowerInvariant()))
                return BadRequest(new { message = $"Invalid report type: {reportType}" });

            if (format.Equals("excel", StringComparison.OrdinalIgnoreCase))
            {
                var bytes = await _exportService.ExportReportToExcelAsync(reportType, ct);
                return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"{reportType}_Report_{DateTime.Now:yyyyMMdd}.xlsx");
            }
            else if (format.Equals("pdf", StringComparison.OrdinalIgnoreCase))
            {
                var bytes = await _exportService.ExportReportToPdfAsync(reportType, ct);
                return File(bytes, "application/pdf", $"{reportType}_Report_{DateTime.Now:yyyyMMdd}.pdf");
            }
            return BadRequest(new { message = "Unsupported format. Use 'excel' or 'pdf'." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export report {ReportType} as {Format}", reportType, format);
            return BadRequest(new { message = "Export failed. Ensure Playwright is installed: dotnet tool install --global Microsoft.Playwright.CLI && playwright install" });
        }
    }

    // ─────────────────────────────────────────────────────────────────
    //  Auto-Renewal
    // ─────────────────────────────────────────────────────────────────

    [HttpPost("Contracts/CancelAutoRenewal/{contractId}")]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize("Contract.Edit")]
    public async Task<IActionResult> CancelAutoRenewal(int contractId, CancellationToken ct)
    {
        var currentUserId = GetCurrentUserId();
        var result = await _contractService.CancelAutoRenewalAsync(contractId, currentUserId, ct);
        if (!result)
        {
            TempData["ErrorMessage"] = "عدم امکان لغو تمدید خودکار. قرارداد تمدید خودکار فعال ندارد یا مجاز نیستید.";
        }
        else
        {
            TempData["SuccessMessage"] = "تمدید خودکار با موفقیت لغو شد.";
        }
        return RedirectToAction(nameof(Details), new { id = contractId });
    }

    // ─────────────────────────────────────────────────────────────────
    //  Column Customization
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Saves the user's column visibility preferences for the contracts list.
    /// </summary>
    [HttpPost("Contracts/SaveColumnPreference")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveColumnPreference([FromBody] SaveColumnPreferenceRequest request, CancellationToken ct)
    {
        if (request?.ColumnNames == null)
            return Json(new { success = false, message = "داده‌ای ارسال نشده است." });

        try
        {
            var currentUserId = GetCurrentUserId();
            await _contractService.SaveColumnPreferenceAsync(currentUserId, request.ColumnNames, ct);
            return Json(new { success = true, message = "تنظیمات ستون‌ها ذخیره شد." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save column preference");
            return Json(new { success = false, message = "خطا در ذخیره تنظیمات ستون‌ها." });
        }
    }
}
