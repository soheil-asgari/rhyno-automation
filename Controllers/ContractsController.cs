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
    private readonly ILogger<ContractsController> _logger;

    public ContractsController(IContractService contractService, ILogger<ContractsController> logger)
    {
        _contractService = contractService;
        _logger = logger;
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

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize("Contract.Payment")]
    public async Task<IActionResult> AddPayment(ContractPaymentCreateVM model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "اطلاعات پرداخت معتبر نیست.";
            return RedirectToAction(nameof(Details), new { id = model.ContractId });
        }
        try
        {
            var currentUser = GetCurrentUserId();
            await _contractService.AddPaymentAsync(model, currentUser, ct);
            TempData["SuccessMessage"] = "پرداخت با موفقیت ثبت شد.";
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
            await _contractService.UploadAttachmentAsync(contractId, file.FileName, stream, file.ContentType, currentUserId, ct);
            TempData["SuccessMessage"] = "فایل با موفقیت بارگذاری شد.";
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
            TempData["SuccessMessage"] = "فایل پیوست حذف شد.";
            return RedirectToAction(nameof(Details), new { id = contractId });
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    // ═══════════════════════════════════════════════════════════════
    //  Document Generation
    // ═══════════════════════════════════════════════════════════════

    [HttpGet("Contracts/GenerateDocument/{id}")]
    [PermissionAuthorize("Contract.View")]
    public async Task<IActionResult> GenerateDocument(int id, int? templateId, CancellationToken ct)
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            var html = await _contractService.GenerateContractDocumentAsync(id, templateId, currentUserId, ct);
            return Content(html, "text/html", System.Text.Encoding.UTF8);
        }
        catch (KeyNotFoundException) { return NotFound(); }
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
}