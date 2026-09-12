using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;
using OfficeAutomation.Models;
using OfficeAutomation.Modules.Office.Infrastructure.Persistence;
using OfficeAutomation.Services.Contracts;
using OfficeAutomation.Services.Security;
using System.Security.Claims;
using Hangfire;
using OfficeAutomation.Services;
using OfficeAutomation.Utilities;

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
    private readonly IPaymentScheduleService _paymentScheduleService;
    private readonly IAmendmentService _amendmentService;
    private readonly ITemplateRenderingService _templateRenderer;
    private readonly IWebHostEnvironment _env;
    public ContractsController(IContractService contractService, ISignatureService signatureService, IAttachmentVersionService attachmentVersionService, IContractFieldValidator fieldValidator, IContractReportService reportService, IContractExportService exportService, IPaymentScheduleService paymentScheduleService, IAmendmentService amendmentService, ITemplateRenderingService templateRenderer, ILogger<ContractsController> logger, IWebHostEnvironment env)
    {
        _contractService = contractService;
        _signatureService = signatureService;
        _attachmentVersionService = attachmentVersionService;
        _fieldValidator = fieldValidator;
        _reportService = reportService;
        _exportService = exportService;
        _paymentScheduleService = paymentScheduleService;
        _amendmentService = amendmentService;
        _templateRenderer = templateRenderer;
        _logger = logger;
        _env = env;
    }

    private string GetCurrentUserId()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            throw new UnauthorizedAccessException("کاربر احراز هویت نشده است.");
        }
        return userId;
    }

    private bool IsAjaxRequest()
    {
        return HttpContext.Request.Headers.XRequestedWith == "XMLHttpRequest";
    }

    /// <summary>
    /// Converts Gregorian DateTime to Persian (Shamsi) date string (yyyy/MM/dd).
    /// </summary>
    public static string ToPersianDate(DateTime dt)
    {
        if (dt == DateTime.MinValue || dt.Year < 1700) return string.Empty;
        try
        {
            var pc = new System.Globalization.PersianCalendar();
            return $"{pc.GetYear(dt):0000}/{pc.GetMonth(dt):00}/{pc.GetDayOfMonth(dt):00}";
        }
        catch
        {
            return dt.ToString("yyyy/MM/dd");
        }
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

    // ═══════════════════════════════════════════════════════════════
    //  Index
    // ═══════════════════════════════════════════════════════════════

    [HttpGet]
    public async Task<IActionResult> Index(ContractIndexVM filter, CancellationToken ct)
    {
        var currentUserId = GetCurrentUserId();
        var vm = await _contractService.GetIndexViewModelAsync(filter, currentUserId, ct);
        return View(vm);
    }

    // ═══════════════════════════════════════════════════════════════
    //  Dashboard
    // ═══════════════════════════════════════════════════════════════

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
    // ═══════════════════════════════════════════════════════════════
    //  Create
    // ═══════════════════════════════════════════════════════════════

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
            vm.CurrencyCode = model.CurrencyCode; vm.ExchangeRate = model.ExchangeRate;
            vm.TaxMethod = model.TaxMethod; vm.DefaultTaxRatePercent = model.DefaultTaxRatePercent;
            vm.FieldValues = model.FieldValues;
            vm.TypeFields = model.TypeFields;
            return View(vm);
        }
        try
        {
            var currentUser = GetCurrentUserId();
            var contract = await _contractService.CreateContractAsync(model, currentUser, ct);
            TempData["SuccessMessage"] = "قرارداد با موفقیت ایجاد شد.";
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
            vm.CurrencyCode = model.CurrencyCode; vm.ExchangeRate = model.ExchangeRate;
            vm.TaxMethod = model.TaxMethod; vm.DefaultTaxRatePercent = model.DefaultTaxRatePercent;
            vm.FieldValues = model.FieldValues;
            vm.TypeFields = model.TypeFields;
            return View(vm);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  Edit
    // ═══════════════════════════════════════════════════════════════

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
                vm.CurrencyCode = model.CurrencyCode; vm.ExchangeRate = model.ExchangeRate;
                vm.TaxMethod = model.TaxMethod; vm.DefaultTaxRatePercent = model.DefaultTaxRatePercent;
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
            TempData["SuccessMessage"] = "قرارداد با موفقیت ویرایش شد.";
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

    // ═══════════════════════════════════════════════════════════════
    //  Details
    // ═══════════════════════════════════════════════════════════════

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

    // ═══════════════════════════════════════════════════════════════
    //  Delete
    // ═══════════════════════════════════════════════════════════════

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
        TempData["SuccessMessage"] = "قرارداد با موفقیت حذف شد.";
        return RedirectToAction(nameof(Index));
    }

    // ═══════════════════════════════════════════════════════════════
    //  Archive
    // ═══════════════════════════════════════════════════════════════

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
        TempData["SuccessMessage"] = "قرارداد با موفقیت بایگانی شد.";
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
        TempData["SuccessMessage"] = "قرارداد از بایگانی خارج شد.";
        return RedirectToAction(nameof(Details), new { id });
    }

    // ═══════════════════════════════════════════════════════════════
    //  Payments
    // ═══════════════════════════════════════════════════════════════
    /// <summary>GET: /Contracts/Schedule/{contractId} — returns payment schedule partial view</summary>
    [HttpGet("Contracts/Schedule/{contractId}")]
    [PermissionAuthorize("Contract.View")]
    public async Task<IActionResult> Schedule(int contractId, CancellationToken ct)
    {
        try
        {
            var vm = await _paymentScheduleService.GetScheduleAsync(contractId, ct);
            if (vm == null)
            {
                // Return empty state when no schedule exists — front-end shows the "no schedule" message
                vm = new PaymentScheduleVM
                {
                    ContractId = contractId,
                    InstallmentCount = 1,
                    IntervalDays = 30,
                    StartDate = DateTime.Today,
                    Installments = new List<PaymentScheduleInstallmentVM>()
                };
            }
            return PartialView("~/Views/Contracts/_PaymentSchedule.cshtml", vm);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load payment schedule for contract {ContractId}", contractId);
            return Content("<div class=\"text-center py-4 text-[11px] text-rose-500\"><i class=\"bi bi-exclamation-triangle ml-1\"></i>خطا در بارگذاری برنامه پرداخت.</div>", "text/html");
        }
    }
    // ═══════════════════════════════════════════════════════════════
    //  Payment Schedule — Generate & Delete
    // ═══════════════════════════════════════════════════════════════

    /// <summary>POST: /Contracts/Schedule/Generate — creates a new payment schedule</summary>
    [HttpPost("Contracts/Schedule/Generate")]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize("Contract.Payment")]
    public async Task<IActionResult> GenerateSchedule([FromForm] PaymentScheduleVM model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "اطلاعات برنامه پرداخت معتبر نیست.";
            return RedirectToAction(nameof(Details), new { id = model.ContractId });
        }

        try
        {
            var currentUser = GetCurrentUserId();
            await _paymentScheduleService.GenerateScheduleAsync(model, currentUser, ct);
            TempData["SuccessMessage"] = "برنامه پرداخت با موفقیت ایجاد شد.";
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate payment schedule for contract {ContractId}", model.ContractId);
            TempData["ErrorMessage"] = "خطا در ایجاد برنامه پرداخت: " + ex.Message;
        }

        return RedirectToAction(nameof(Details), new { id = model.ContractId });
    }

    /// <summary>POST: /Contracts/Schedule/Delete/{scheduleId} — deletes a payment schedule</summary>
    [HttpPost("Contracts/Schedule/Delete/{scheduleId}")]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize("Contract.Payment")]
    public async Task<IActionResult> DeleteSchedule(int scheduleId, int contractId, CancellationToken ct)
    {
        try
        {
            var currentUser = GetCurrentUserId();
            var deleted = await _paymentScheduleService.DeleteScheduleAsync(scheduleId, currentUser, ct);
            if (!deleted) return NotFound();
            TempData["SuccessMessage"] = "برنامه پرداخت با موفقیت حذف شد.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete payment schedule {ScheduleId}", scheduleId);
            TempData["ErrorMessage"] = "خطا در حذف برنامه پرداخت.";
        }

        return RedirectToAction(nameof(Details), new { id = contractId });
    }

    // ═══════════════════════════════════════════════════════════════
    //  Payments
    // ═══════════════════════════════════════════════════════════════



    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize("Contract.Payment")]
    public async Task<IActionResult> AddPayment(ContractPaymentCreateVM model, CancellationToken ct)
    {
        var isAjax = IsAjaxRequest();

        // If dates arrived as Persian date strings (from hidden fields that bypass model binding),
        // parse them to Gregorian DateTime here as a safety net.
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

        // Strict validation: monetary amounts must be greater than zero
        if (model.Amount <= 0)
        {
            ModelState.AddModelError("Amount", "مبلغ پرداخت باید بزرگتر از صفر باشد.");
        }

        // Required fields: payment date must be valid
        if (model.PaymentDate == default || model.PaymentDate.Year < 1700)
        {
            ModelState.AddModelError("PaymentDate", "تاریخ پرداخت الزامی و معتبر است.");
        }

        if (!ModelState.IsValid)
        {
            var errorDict = ModelState
                .Where(kv => kv.Value != null && kv.Value.Errors.Count > 0)
                .ToDictionary(
                    kv => kv.Key,
                    kv => kv.Value!.Errors.First().ErrorMessage
                );

            _logger.LogWarning("AddPayment validation failed for contract {ContractId}: {Errors}",
                model.ContractId, string.Join(" | ", errorDict.Values));

            if (isAjax)
            {
                return Json(new { success = false, errors = errorDict });
            }

            TempData["ErrorMessage"] = string.Join(" | ", errorDict.Values);
            return RedirectToAction(nameof(Details), new { id = model.ContractId, activeTab = "payments" });
        }

        try
        {
            var currentUser = GetCurrentUserId();
            await _contractService.AddPaymentAsync(model, currentUser, ct);
            if (isAjax)
            {
                return Json(new { success = true, message = "پرداخت با موفقیت ثبت شد." });
            }
            TempData["SuccessMessage"] = "پرداخت با موفقیت ثبت شد.";
        }
        catch (KeyNotFoundException)
        {
            if (isAjax) return Json(new { success = false, errors = new Dictionary<string, string> { ["General"] = "قرارداد یافت نشد." } });
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            if (isAjax) return Json(new { success = false, errors = new Dictionary<string, string> { ["General"] = "عدم دسترسی مجاز." } });
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding payment for contract {ContractId}", model.ContractId);
            if (isAjax) return Json(new { success = false, errors = new Dictionary<string, string> { ["General"] = ex.Message } });
            TempData["ErrorMessage"] = $"خطا در ثبت پرداخت: {ex.Message}";
        }

        return RedirectToAction(nameof(Details), new { id = model.ContractId, activeTab = "payments" });
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
            TempData["SuccessMessage"] = "پرداخت با موفقیت حذف شد.";
            return RedirectToAction(nameof(Details), new { id = contractId });
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    // ═══════════════════════════════════════════════════════════════
    //  Attachments
    // ═══════════════════════════════════════════════════════════════

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize("Contract.Edit")]
    public async Task<IActionResult> UploadAttachment(int contractId, IFormFile file, CancellationToken ct)
    {
        if (file == null || file.Length == 0)
        {
            TempData["ErrorMessage"] = "فایل انتخاب نشده است.";
            return RedirectToAction(nameof(Details), new { id = contractId });
        }

        try
        {
            var currentUserId = GetCurrentUserId();
            await using var stream = file.OpenReadStream();
            var attachment = await _contractService.UploadAttachmentAsync(contractId, file.FileName, stream, file.ContentType, currentUserId, ct);

            // Add audit log
            var attachmentName = attachment?.FileName ?? file?.FileName ?? "نامشخص";
            await _contractService.AddAuditLogAsync(contractId, currentUserId, "AttachmentUploaded", null, null, $"فایل {attachmentName} بارگذاری شد", ct);

            var isAjax = HttpContext.Request.Headers.XRequestedWith == "XMLHttpRequest";
            if (isAjax)
            {
                var userName = User.FindFirstValue("FullName") ?? User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "نامشخص";
                return Json(new
                {
                    success = true,
                    attachment = new
                    {
                        id = attachment!.Id,
                        fileName = attachment.FileName,
                        contentType = attachment.ContentType ?? "application/octet-stream",
                        fileSize = attachment.FileSize,
                        createdAt = attachment.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
                        uploadedByUserName = userName
                    }
                });
            }

            TempData["SuccessMessage"] = "فایل با موفقیت بارگذاری شد.";
        }
        catch (KeyNotFoundException) { if (IsAjaxRequest()) return Json(new { success = false, message = "قرارداد یافت نشد." }); return NotFound(); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload attachment for contract {ContractId}", contractId);
            if (IsAjaxRequest())
                return Json(new { success = false, message = "خطا در بارگذاری فایل: " + ex.Message });
            TempData["ErrorMessage"] = ex.Message;
        }
        return RedirectToAction(nameof(Details), new { id = contractId });
    }

    [EnableRateLimiting("attachment-download")]
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
            var att = await _contractService.GetAttachmentAsync(attachmentId, currentUser, ct);
            var fileName = att?.FileName ?? "نامشخص";
            // Resolve the actual contractId from the attachment to handle mismatches
            var actualContractId = att?.ContractId ?? contractId;
            var deleted = await _contractService.DeleteAttachmentAsync(attachmentId, actualContractId, currentUser, ct);
            if (!deleted)
            {
                // Try deleting by attachmentId only (ignore contractId mismatch)
                await _contractService.DeleteAttachmentByIdOnlyAsync(attachmentId, currentUser, ct);
            }

            // Add audit log
            await _contractService.AddAuditLogAsync(actualContractId, currentUser, "AttachmentDeleted", null, null, $"فایل {fileName} حذف شد", ct);

            if (IsAjaxRequest())
                return Json(new { success = true, message = "فایل پیوست حذف شد." });

            TempData["SuccessMessage"] = "فایل پیوست حذف شد.";
            return RedirectToAction(nameof(Details), new { id = contractId });
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete attachment {AttachmentId}", attachmentId);
            if (IsAjaxRequest())
                return Json(new { success = false, message = "خطا در حذف فایل." });
            TempData["ErrorMessage"] = "خطا در حذف فایل.";
            return RedirectToAction(nameof(Details), new { id = contractId });
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  Document Generation
    // ═══════════════════════════════════════════════════════════════

    [HttpGet("Contracts/GenerateDocument/{id}")]
    [PermissionAuthorize("Contract.View")]
    public async Task<IActionResult> GenerateDocument(int id, CancellationToken ct)
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            var vm = await _contractService.GetDetailsViewModelAsync(id, currentUserId, ct);
            if (!string.IsNullOrWhiteSpace(vm.ContractBodyHtml))
            {
                vm.ContractBodyHtml = _templateRenderer.RenderTemplate(vm.ContractBodyHtml, vm);
            }
            if (!string.IsNullOrWhiteSpace(vm.TermsAndConditions))
            {
                vm.TermsAndConditions = _templateRenderer.RenderTemplate(vm.TermsAndConditions, vm);
            }
            if (!string.IsNullOrWhiteSpace(vm.Description))
            {
                vm.Description = _templateRenderer.RenderTemplate(vm.Description, vm);
            }
            ViewData["Title"] = "چاپ / PDF قرارداد";
            return View("GenerateDocument", vm);
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    [HttpGet("Contracts/ExportToDocx/{id}")]
    [PermissionAuthorize("Contract.View")]
    public async Task<IActionResult> ExportToDocx(int id, CancellationToken ct)
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            var docxBytes = await _exportService.ExportContractToDocxAsync(id, currentUserId, ct);
            var contract = await _contractService.GetDetailsViewModelAsync(id, currentUserId, ct);
            var fileName = $"Contract_{contract.ContractNumber ?? id.ToString()}.docx";
            return File(docxBytes, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", fileName);
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    // ═══════════════════════════════════════════════════════════════
    //  Settings / Templates
    // ═══════════════════════════════════════════════════════════════

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
        TempData["SuccessMessage"] = "تنظیمات با موفقیت ذخیره شد.";
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
        TempData["SuccessMessage"] = "قالب با موفقیت ذخیره شد.";
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
        TempData["SuccessMessage"] = "قالب با موفقیت حذف شد.";
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
        TempData["SuccessMessage"] = "قالب پیش‌فرض با موفقیت تنظیم شد.";
        return RedirectToAction(nameof(Settings));
    }

    // ═══════════════════════════════════════════════════════════════
    //  Template Preview (for auto-populate in Create/Edit)
    // ═══════════════════════════════════════════════════════════════

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
    // ═══════════════════════════════════════════════════════════════════════
    //  E-Signature: Internal Sign
    // ═══════════════════════════════════════════════════════════════════════

    [HttpPost("Contracts/{contractId}/Sign")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SignContract(int contractId, [FromBody] SignContractRequest request, CancellationToken ct)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.SignatureImageData))
            return Json(new { success = false, message = "داده‌های امضا خالی است." });

        try
        {
            var userId = GetCurrentUserId();
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "0.0.0.0";
            var userAgent = Request.Headers.UserAgent.ToString();

            var signer = await _signatureService.CaptureInternalSignatureAsync(
                contractId, request.SignatureImageData, request.Comment, ipAddress, userAgent, userId, request.SignerParty, request.SignerName, ct);

            return Json(new { success = true, message = "امضا با موفقیت ثبت شد", signerId = signer.Id, signedAt = signer.SignedAt, signerParty = signer.JobTitle, signerName = signer.FullName });
        }
        catch (Exception ex)
        {
            var innerMsg = ex.InnerException?.Message ?? ex.Message;
            _logger.LogError(ex, "Failed to sign contract {ContractId}: {InnerMessage}", contractId, innerMsg);
            return Json(new { success = false, message = $"خطا در ثبت امضا: {innerMsg}" });
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  E-Signature: Get External Sign Link
    // ═══════════════════════════════════════════════════════════════════════

    [HttpPost("Contracts/{contractId}/GetExternalSignLink")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GetExternalSignLink(int contractId, [FromBody] GetExternalSignLinkRequest request, CancellationToken ct)
    {
        if (request == null || request.SignerId <= 0)
            return Json(new { success = false, message = "شناسه امضاکننده نامعتبر است." });

        try
        {
            var link = await _signatureService.GenerateExternalSignerLinkAsync(contractId, request.SignerId, ct);
            return Json(new { success = true, link });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate external sign link for contract {ContractId}", contractId);
            return Json(new { success = false, message = $"خطا در تولید لینک: {ex.Message}" });
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  E-Signature: Create Signature Request
    // ═══════════════════════════════════════════════════════════════════════

    [HttpPost("Contracts/Signature/CreateRequest/{contractId}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateSignatureRequest(int contractId, [FromBody] CreateSignatureRequestVm model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return Json(new { success = false, message = "اطلاعات نامعتبر است." });

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
            return Json(new { success = false, message = $"خطا در ایجاد درخواست: {ex.Message}" });
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  E-Signature: Get Signature Requests
    // ═══════════════════════════════════════════════════════════════════════

    [HttpGet("Contracts/{contractId}/SignatureRequests")]
    public async Task<IActionResult> GetSignatureRequests(int contractId, CancellationToken ct)
    {
        try
        {
            var requests = await _signatureService.GetSignatureRequestsAsync(contractId, ct);
            var data = requests.Select(r => new
            {
                r.Id,
                r.Title,
                r.Status,
                r.DeliveryMethod,
                r.SigningOrder,
                r.AutoActivateContract,
                r.CreatedAt,
                r.CompletedAt,
                Signers = r.Signers.Select(s => new
                {
                    s.Id,
                    s.UserId,
                    UserName = s.User?.FullName ?? s.FullName ?? s.Email ?? "نامشخص",
                    s.SignerType,
                    s.Result,
                    s.SignOrder,
                    s.SignedAt,
                    hasSignatureImage = s.SignatureImageData != null
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
            return Json(new { success = false, message = "خطا در بازیابی نسخه‌ها." });
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
                return Json(new { success = false, message = "فایلی انتخاب نشده است." });

            await using var stream = file.OpenReadStream();
            var version = await _attachmentVersionService.UploadNewVersionAsync(
                attachmentId, file.FileName, file.ContentType ?? "application/octet-stream",
                file.Length, stream, userId, changeDescription, ct);

            return Json(new { success = true, versionId = version.Id, versionNumber = version.VersionNumber });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload new version for attachment {AttachmentId}", attachmentId);
            return Json(new { success = false, message = "خطا در بارگذاری نسخه جدید: " + ex.Message });
        }
    }

    [EnableRateLimiting("attachment-download")]
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
            return Json(new { success = false, message = "خطا در بازگردانی نسخه: " + ex.Message });
        }
    }
    // ════════════════════════════════════════════════════════════
    //  Attachment Preview
    // ════════════════════════════════════════════════════════════

    private static readonly HashSet<string> PreviewableImageExts = new(
        [".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff", ".tif", ".webp", ".svg"],
        StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> PreviewableTextExts = new(
        [".txt", ".csv", ".log", ".json", ".xml", ".md", ".yaml", ".yml", ".ini", ".cfg", ".config"],
        StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Returns file content for previewing an attachment in the browser.
    /// For PDF/images/text, returns the file stream directly.
    /// For unsupported types, returns a JSON response with previewUnavailable = true.
    /// </summary>
    [HttpGet("Contracts/Attachment/{attachmentId}/Preview")]
    public async Task<IActionResult> PreviewAttachment(int attachmentId, CancellationToken ct)
    {
        try
        {
            var userId = GetCurrentUserId();
            var attachment = await _contractService.GetAttachmentAsync(attachmentId, userId, ct);

            if (attachment == null)
            {
                _logger.LogWarning("Attachment {AttachmentId} not found for preview", attachmentId);
                return CreateUnavailableResult("پیوست مورد نظر یافت نشد.");
            }

            // یافتن مسیر فیزیکی فایل
            string? filePath = await ResolveFilePathAsync(attachment, userId, ct);

            if (string.IsNullOrEmpty(filePath) || !System.IO.File.Exists(filePath))
            {
                _logger.LogWarning("Preview file not found for attachment {AttachmentId}, CurrentVersionId={VersionId}",
                    attachmentId, attachment.CurrentVersionId);

                return CreateUnavailableResult(
                    "فایل پیوست در سرور یافت نشد. ممکن است حذف شده باشد.",
                    attachment.FileName, attachment.FileSize, attachment.ContentType);
            }

            var fileName = attachment.FileName;
            var ext = Path.GetExtension(fileName);
            var contentType = attachment.ContentType;

            // ۱. فایل‌های PDF با پشتیبانی از HTTP 206 Partial Content
            if (".pdf".Equals(ext, StringComparison.OrdinalIgnoreCase))
            {
                // اگر درخواست Blob از سمت JS است (query param ?blob=1)
                // با Content-Type خنثی سرو کنیم تا IDM نتواند رهگیری کند
                if (Request.Query.ContainsKey("blob"))
                {
                    return PhysicalFile(filePath, "application/octet-stream", enableRangeProcessing: false);
                }
                SetInlineContentDisposition(fileName);
                return PhysicalFile(filePath, contentType ?? "application/pdf", enableRangeProcessing: true);
            }

            // ۲. تصاویر
            if (PreviewableImageExts.Contains(ext))
            {
                var imageContentType = GetImageContentType(ext, contentType);
                SetInlineContentDisposition(fileName);
                return PhysicalFile(filePath, imageContentType);
            }

            // ۳. متون متنی (محدود به 500KB)
            if (PreviewableTextExts.Contains(ext))
            {
                var text = await System.IO.File.ReadAllTextAsync(filePath, ct);
                if (text.Length > 512000)
                {
                    text = text[..512000] + "\n\n... (فایل بزرگتر از حد مجاز برای نمایش است)";
                }
                return Content(text, "text/plain; charset=utf-8");
            }

            // ۴. فرمت‌های غیرقابل پیش‌نمایش مستقیم
            return CreateUnavailableResult("پیش‌نمایش برای این نوع فایل در دسترس نیست.", fileName, attachment.FileSize, contentType);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Parent contract not found for attachment {AttachmentId}", attachmentId);
            return CreateUnavailableResult("فایل پیوست در سرور یافت نشد. ممکن است حذف شده باشد.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to preview attachment {AttachmentId}", attachmentId);

            return Ok(new
            {
                success = false,
                previewUnavailable = true,
                message = "خطای غیرمنتظره‌ای در بارگذاری پیش‌نمایش رخ داد.",
                details = _env.IsDevelopment() ? ex.ToString() : null
            });
        }
    }

    #region Helper Methods

    // تغییر ورودی به مدل OfficeAutomation.Models.ContractAttachment و اصلاح تایپ userId به dynamic/string
    private async Task<string?> ResolveFilePathAsync(OfficeAutomation.Models.ContractAttachment attachment, dynamic userId, CancellationToken ct)
    {
        // 1. Current Version Path
        if (attachment.CurrentVersionId.HasValue)
        {
            var versionPath = await _attachmentVersionService.GetVersionFilePathAsync(attachment.CurrentVersionId.Value, userId, ct);
            if (!string.IsNullOrEmpty(versionPath) && System.IO.File.Exists(versionPath))
                return versionPath;
        }

        // 2. Relative Path (با ایمن‌سازی کامل Path Traversal)
        if (!string.IsNullOrEmpty(attachment.RelativePath))
        {
            var root = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "contracts"))
                       + Path.DirectorySeparatorChar;
            var candidate = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", attachment.RelativePath.TrimStart('/')));

            if (candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase) && System.IO.File.Exists(candidate))
            {
                return candidate;
            }
        }

        // 3. Stored File Name Scan
        if (!string.IsNullOrEmpty(attachment.StoredFileName))
        {
            var contractDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "contracts", attachment.ContractId.ToString());
            if (Directory.Exists(contractDir))
            {
                var directFile = Directory.GetFiles(contractDir)
                    .FirstOrDefault(f => Path.GetFileName(f).Equals(attachment.StoredFileName, StringComparison.OrdinalIgnoreCase));

                if (directFile != null) return directFile;
            }
        }

        return null;
    }

    private void SetInlineContentDisposition(string fileName)
    {
        var disposition = new ContentDispositionHeaderValue("inline")
        {
            FileName = fileName,
            FileNameStar = fileName
        };
        Response.Headers[HeaderNames.ContentDisposition] = disposition.ToString();
    }

    private static string GetImageContentType(string ext, string? fallbackContentType)
    {
        if (!string.IsNullOrEmpty(fallbackContentType) && fallbackContentType != "application/octet-stream")
            return fallbackContentType;

        return ext switch
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

    private IActionResult CreateUnavailableResult(string message, string? fileName = null, long? fileSize = null, string? contentType = null)
    {
        return Ok(new
        {
            success = false,
            previewUnavailable = true,
            message,
            fileName,
            fileSize,
            contentType
        });
    }

    #endregion

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
            else if (format.Equals("pdf", StringComparison.OrdinalIgnoreCase))
            {
                var bytes = await _exportService.ExportContractsToPdfAsync(filters, userId, ct);
                return File(bytes, "application/pdf", $"Contracts_{DateTime.Now:yyyyMMdd}.pdf");
            }
            return BadRequest(new { message = "Unsupported format. Use 'excel' or 'pdf'." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export contracts");
            return BadRequest(new { message = $"Export failed: {ex.Message}" });
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

    /// <summary>
    /// Admin view showing upcoming/executed auto-renewal jobs, scheduler checkpoints, and history.
    /// </summary>
    [HttpGet("Contracts/AutoRenewals")]
    [PermissionAuthorize("Contract.View")]
    public async Task<IActionResult> AutoRenewals(CancellationToken ct)
    {
        var vm = await _contractService.GetAutoRenewalAdminViewModelAsync(ct);
        return View(vm);
    }

    /// <summary>
    /// Manually triggers immediate auto-renewal processing via Hangfire one-off background job.
    /// </summary>
    [HttpPost("Contracts/TriggerAutoRenewalsNow")]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize("Contract.Edit")]
    public IActionResult TriggerAutoRenewalsNow()
    {
        try
        {
            var jobId = BackgroundJob.Enqueue<ContractHangfireJobs>(j => j.ProcessAutoRenewalsAsync(CancellationToken.None));
            TempData["SuccessMessage"] = $"جاب تمدید خودکار با موفقیت در صف اجرا قرار گرفت (Job ID: {jobId}).";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enqueue immediate auto-renewal Hangfire job.");
            TempData["ErrorMessage"] = $"خطا در ارسال جاب به صف اجرا: {ex.Message}";
        }
        return RedirectToAction(nameof(AutoRenewals));
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

    // ═══════════════════════════════════════════════════════════════
    //  Repair / Cleanup
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Removes orphaned attachment DB records whose physical file no longer exists on disk.
    /// Restricted to admin users. Returns removed count.
    /// </summary>
    [HttpPost("Contracts/CleanupOrphanedAttachments/{contractId?}")]
    [PermissionAuthorize("Contract.Delete")]
    public async Task<IActionResult> CleanupOrphanedAttachments(int? contractId, CancellationToken ct)
    {
        try
        {
            var currentUser = GetCurrentUserId();
            var count = await _contractService.CleanupOrphanedAttachmentsAsync(contractId, currentUser, ct);
            return Json(new { success = true, removedCount = count, message = $"{count} پیوست یتیم حذف شد." });
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cleanup orphaned attachments");
            return Json(new { success = false, message = "خطا در پاکسازی: " + ex.Message });
        }
    }

    // ════════════════════════════════════════════════════════════
    //  Amendments Actions
    // ════════════════════════════════════════════════════════════

    [HttpPost("Contracts/CreateAmendment/{contractId}")]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize("Contract.Edit")]
    public async Task<IActionResult> CreateAmendment(int contractId, AmendmentCreateVM model, IFormFile? attachmentFile, CancellationToken ct)
    {
        var isAjax = IsAjaxRequest();
        model.ContractId = contractId;
        var userId = GetCurrentUserId();

        if (Request.Form.TryGetValue("EffectiveDate", out var effDateRaw) && !string.IsNullOrWhiteSpace(effDateRaw))
        {
            var parsed = ToGregorianDate(effDateRaw);
            if (parsed.HasValue)
            {
                model.EffectiveDate = parsed.Value;
                ModelState.Remove("EffectiveDate");
            }
        }

        if (Request.Form.TryGetValue("NewEndDate", out var newEndDateRaw) && !string.IsNullOrWhiteSpace(newEndDateRaw))
        {
            var parsed = ToGregorianDate(newEndDateRaw);
            if (parsed.HasValue)
            {
                model.NewEndDate = parsed.Value;
                ModelState.Remove("NewEndDate");
            }
        }

        // Strict validation: Title is required
        if (string.IsNullOrWhiteSpace(model.Title))
        {
            ModelState.AddModelError("Title", "عنوان اصلاحیه الزامی است.");
        }

        // Strict validation: EffectiveDate is required
        if (model.EffectiveDate == default || model.EffectiveDate.Year < 1700)
        {
            ModelState.AddModelError("EffectiveDate", "تاریخ مؤثر اصلاحیه الزامی و معتبر است.");
        }

        // Validation for specific amendment types
        if (model.AmendmentType == "AmountIncrease" && model.AmountChange <= 0)
        {
            ModelState.AddModelError("AmountChange", "برای افزایش مبلغ قرارداد، مبلغ باید بزرگتر از صفر باشد.");
        }
        else if (model.AmendmentType == "AmountDecrease" && model.AmountChange >= 0)
        {
            ModelState.AddModelError("AmountChange", "برای کاهش مبلغ قرارداد، مبلغ باید منفی باشد.");
        }

        if (!ModelState.IsValid)
        {
            var errorDict = ModelState
                .Where(kv => kv.Value != null && kv.Value.Errors.Count > 0)
                .ToDictionary(
                    kv => kv.Key,
                    kv => kv.Value!.Errors.First().ErrorMessage
                );

            _logger.LogWarning("CreateAmendment validation failed for contract {ContractId}: {Errors}",
                contractId, string.Join(" | ", errorDict.Values));

            if (isAjax)
            {
                return Json(new { success = false, errors = errorDict, message = string.Join(" | ", errorDict.Values) });
            }

            TempData["ErrorMessage"] = string.Join(" | ", errorDict.Values);
            return RedirectToAction(nameof(Details), new { id = contractId, activeTab = "amendments" });
        }

        try
        {
            var amendment = await _amendmentService.CreateAmendmentAsync(model, userId, ct);

            if (attachmentFile != null && attachmentFile.Length > 0)
            {
                using var stream = attachmentFile.OpenReadStream();
                await _contractService.UploadAttachmentAsync(contractId, attachmentFile.FileName, stream, attachmentFile.ContentType, userId, ct);
            }

            if (isAjax)
            {
                return Json(new { success = true, message = "اصلاحیه با موفقیت ثبت شد." });
            }

            TempData["SuccessMessage"] = "اصلاحیه با موفقیت ثبت شد.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating amendment for contract {ContractId}", contractId);
            if (isAjax)
            {
                return Json(new { success = false, errors = new Dictionary<string, string> { ["General"] = ex.Message }, message = $"خطا در ثبت اصلاحیه: {ex.Message}" });
            }
            TempData["ErrorMessage"] = $"خطا در ثبت اصلاحیه: {ex.Message}";
        }

        return RedirectToAction(nameof(Details), new { id = contractId, activeTab = "amendments" });
    }

    [HttpPost("Contracts/SubmitAmendmentForApproval/{amendmentId}")]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize("Contract.Edit")]
    public async Task<IActionResult> SubmitAmendmentForApproval(int amendmentId, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        try
        {
            var result = await _amendmentService.SubmitForApprovalAsync(amendmentId, userId, ct);
            if (result != null)
                TempData["SuccessMessage"] = "اصلاحیه برای تایید ارسال شد.";
            else
                TempData["ErrorMessage"] = "امکان ارسال اصلاحیه برای تایید وجود ندارد.";

            return RedirectToAction(nameof(Details), new { id = result?.ContractId ?? 0, activeTab = "amendments" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting amendment {AmendmentId} for approval", amendmentId);
            TempData["ErrorMessage"] = $"خطا در ارسال برای تایید: {ex.Message}";
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost("Contracts/ApplyAmendment/{amendmentId}")]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize("Contract.Edit")]
    public async Task<IActionResult> ApplyAmendment(int amendmentId, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        try
        {
            var result = await _amendmentService.ApplyAmendmentAsync(amendmentId, userId, ct);
            if (result != null)
                TempData["SuccessMessage"] = "اصلاحیه با موفقیت روی قرارداد اعمال شد.";
            else
                TempData["ErrorMessage"] = "امکان اعمال اصلاحیه وجود ندارد.";

            return RedirectToAction(nameof(Details), new { id = result?.ContractId ?? 0, activeTab = "amendments" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying amendment {AmendmentId}", amendmentId);
            TempData["ErrorMessage"] = $"خطا در اعمال اصلاحیه: {ex.Message}";
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost("Contracts/CancelAmendment/{amendmentId}")]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize("Contract.Edit")]
    public async Task<IActionResult> CancelAmendment(int amendmentId, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        try
        {
            var result = await _amendmentService.CancelAmendmentAsync(amendmentId, userId, ct);
            if (result != null)
                TempData["SuccessMessage"] = "اصلاحیه لغو شد.";
            else
                TempData["ErrorMessage"] = "امکان لغو اصلاحیه وجود ندارد.";

            return RedirectToAction(nameof(Details), new { id = result?.ContractId ?? 0, activeTab = "amendments" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling amendment {AmendmentId}", amendmentId);
            TempData["ErrorMessage"] = $"خطا در لغو اصلاحیه: {ex.Message}";
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpGet("Contracts/{id}/VersionSnapshots")]
    [PermissionAuthorize("Contract.View")]
    public async Task<IActionResult> GetVersionSnapshots(
        int id,
        [FromServices] OfficeDbContext db,
        [FromServices] IVersionService versionService,
        CancellationToken ct)
    {
        var currentUserId = GetCurrentUserId();
        await _contractService.EnforceAccessAsync(id, currentUserId, ct);

        var contract = await db.Contracts.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, ct);
        if (contract == null) return NotFound();

        var list = new List<object>();

        // 1. Current live version (represented by versionId = 0)
        var currentUpdated = contract.UpdatedAt != default ? contract.UpdatedAt : contract.CreatedAt;
        var currentPersian = ToPersianDate(currentUpdated);
        list.Add(new
        {
            id = 0,
            label = $"نسخه جاری (زنده) - {currentPersian}",
            type = "Current",
            majorVersion = 0,
            minorVersion = 0,
            createdAt = currentUpdated,
            createdAtPersian = currentPersian
        });

        // 2. Stored versions (amendments, edits, snapshots)
        var versions = await versionService.GetVersionsForContractAsync(id, ct);
        foreach (var v in versions)
        {
            var pDate = ToPersianDate(v.CreatedAt);
            var descPart = !string.IsNullOrWhiteSpace(v.ChangeDescription) ? $" ({v.ChangeDescription})" : "";
            var typeLabel = v.ChangeType switch
            {
                "Amendment" => "اصلاحیه",
                "Created" => "نسخه اولیه",
                "Edited" => "ویرایش",
                "StatusChange" => "تغییر وضعیت",
                _ => v.ChangeType
            };

            list.Add(new
            {
                id = v.Id,
                label = $"نسخه {v.VersionLabel} [{typeLabel}] - {pDate}{descPart}",
                type = v.ChangeType,
                majorVersion = v.MajorVersion,
                minorVersion = v.MinorVersion,
                createdAt = v.CreatedAt,
                createdAtPersian = pDate
            });
        }

        return Json(new { success = true, data = list });
    }

    [HttpGet("Contracts/{id}/VersionContent")]
    [PermissionAuthorize("Contract.View")]
    public async Task<IActionResult> GetVersionContent(
        int id,
        [FromQuery] int versionId,
        [FromServices] OfficeDbContext db,
        [FromServices] IVersionService versionService,
        CancellationToken ct)
    {
        var currentUserId = GetCurrentUserId();
        await _contractService.EnforceAccessAsync(id, currentUserId, ct);

        var contract = await db.Contracts.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, ct);
        if (contract == null) return NotFound();

        if (versionId == 0)
        {
            // Current live state
            var bodyHtml = contract.ContractBodyHtml ?? string.Empty;
            var plainText = HtmlSanitizer.StripTags(bodyHtml);
            if (string.IsNullOrWhiteSpace(plainText) && !string.IsNullOrWhiteSpace(contract.TermsAndConditions))
            {
                plainText = contract.TermsAndConditions;
            }

            return Json(new
            {
                success = true,
                versionId = 0,
                versionLabel = "جاری",
                title = contract.Title,
                contractNumber = contract.ContractNumber,
                totalAmount = contract.TotalAmount,
                startDate = contract.StartDate.ToString("yyyy-MM-dd"),
                startDatePersian = ToPersianDate(contract.StartDate),
                endDate = contract.EndDate?.ToString("yyyy-MM-dd"),
                endDatePersian = contract.EndDate.HasValue ? ToPersianDate(contract.EndDate.Value) : null,
                bodyHtml = bodyHtml,
                bodyText = plainText
            });
        }

        // Stored snapshot
        var v = await versionService.GetVersionByIdAsync(versionId, ct);
        if (v == null || v.ContractId != id)
        {
            return Json(new { success = false, message = "نسخه مورد نظر یافت نشد." });
        }

        var snapshot = System.Text.Json.JsonSerializer.Deserialize<ContractSnapshot>(
            v.SnapshotData,
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        ) ?? new ContractSnapshot();

        var snapHtml = snapshot.ContractBodyHtml ?? string.Empty;
        var snapText = HtmlSanitizer.StripTags(snapHtml);
        if (string.IsNullOrWhiteSpace(snapText) && !string.IsNullOrWhiteSpace(snapshot.TermsAndConditions))
        {
            snapText = snapshot.TermsAndConditions;
        }

        return Json(new
        {
            success = true,
            versionId = v.Id,
            versionLabel = v.VersionLabel,
            title = snapshot.Title,
            contractNumber = snapshot.ContractNumber,
            totalAmount = snapshot.TotalAmount,
            startDate = snapshot.StartDate.ToString("yyyy-MM-dd"),
            startDatePersian = ToPersianDate(snapshot.StartDate),
            endDate = snapshot.EndDate?.ToString("yyyy-MM-dd"),
            endDatePersian = snapshot.EndDate.HasValue ? ToPersianDate(snapshot.EndDate.Value) : null,
            bodyHtml = snapHtml,
            bodyText = snapText
        });
    }

    [HttpGet("Contracts/GetAmendmentRedline/{amendmentId}")]
    [PermissionAuthorize("Contract.View")]
    public async Task<IActionResult> GetAmendmentRedline(int amendmentId, [FromServices] OfficeDbContext db, [FromServices] IContractDiffService diffService, CancellationToken ct)
    {
        var amendment = await db.ContractAmendments
            .Include(a => a.Contract)
            .FirstOrDefaultAsync(a => a.Id == amendmentId, ct);

        if (amendment == null)
            return Json(new { success = false, message = "اصلاحیه یافت نشد." });

        var oldText = amendment.Contract?.ContractBodyHtml
            ?? amendment.Contract?.TermsAndConditions
            ?? amendment.Contract?.Description;

        var newText = !string.IsNullOrWhiteSpace(amendment.Description)
            ? amendment.Description
            : oldText;

        var diffHtml = diffService.GenerateRedlineDiff(oldText, newText);

        return Json(new
        {
            success = true,
            amendmentTitle = amendment.Title,
            amendmentNumber = amendment.AmendmentNumber,
            diffHtml = diffHtml
        });
    }
}
