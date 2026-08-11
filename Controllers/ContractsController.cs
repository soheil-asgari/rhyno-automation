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

    // ═══════════════════════════════════════════════════════════════
    //  Index
    // ═══════════════════════════════════════════════════════════════

    [HttpGet]
    public async Task<IActionResult> Index(ContractIndexVM filter, CancellationToken ct)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var vm = await _contractService.GetIndexViewModelAsync(filter, currentUserId, ct);
        return View(vm);
    }

    // ═══════════════════════════════════════════════════════════════
    //  Dashboard
    // ═══════════════════════════════════════════════════════════════

    [HttpGet("Contracts/Dashboard")]
    public async Task<IActionResult> Dashboard(CancellationToken ct)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
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
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
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
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var vm = await _contractService.BuildCreateViewModelAsync(currentUserId, ct);
            vm.Title = model.Title; vm.ContractNumber = model.ContractNumber; vm.ClientName = model.ClientName;
            vm.ClientPhone = model.ClientPhone; vm.ClientAddress = model.ClientAddress; vm.Description = model.Description;
            vm.TermsAndConditions = model.TermsAndConditions; vm.Notes = model.Notes;
            vm.TotalAmount = model.TotalAmount; vm.StartDate = model.StartDate; vm.EndDate = model.EndDate;
            vm.SigningDate = model.SigningDate; vm.RenewalDate = model.RenewalDate; vm.ExpirationDate = model.ExpirationDate;
            vm.Status = model.Status; vm.ResponsibleUserId = model.ResponsibleUserId; vm.ContractTemplateId = model.ContractTemplateId;
            vm.CustomerId = model.CustomerId; vm.ContractType = model.ContractType;
            return View(vm);
        }
        try
        {
            var currentUser = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var contract = await _contractService.CreateContractAsync(model, currentUser, ct);
            TempData["SuccessMessage"] = "قرارداد با موفقیت ایجاد شد.";
            return RedirectToAction(nameof(Details), new { id = contract.Id });
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError("ContractNumber", ex.Message);
            var currentUser = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var vm = await _contractService.BuildCreateViewModelAsync(currentUser, ct);
            vm.Title = model.Title; vm.ContractNumber = model.ContractNumber; vm.ClientName = model.ClientName;
            vm.ClientPhone = model.ClientPhone; vm.ClientAddress = model.ClientAddress; vm.Description = model.Description;
            vm.TermsAndConditions = model.TermsAndConditions; vm.Notes = model.Notes;
            vm.TotalAmount = model.TotalAmount; vm.StartDate = model.StartDate; vm.EndDate = model.EndDate;
            vm.SigningDate = model.SigningDate; vm.RenewalDate = model.RenewalDate; vm.ExpirationDate = model.ExpirationDate;
            vm.Status = model.Status; vm.ResponsibleUserId = model.ResponsibleUserId; vm.ContractTemplateId = model.ContractTemplateId;
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
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var vm = await _contractService.BuildEditViewModelAsync(id, currentUserId, ct);
            return View(vm);
        }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize("Contract.Edit")]
    public async Task<IActionResult> Edit(ContractEditVM model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            try
            {
                var vm = await _contractService.BuildEditViewModelAsync(model.Id, currentUserId, ct);
                vm.Title = model.Title; vm.ContractNumber = model.ContractNumber; vm.ClientName = model.ClientName;
                vm.ClientPhone = model.ClientPhone; vm.ClientAddress = model.ClientAddress; vm.Description = model.Description;
                vm.TermsAndConditions = model.TermsAndConditions; vm.Notes = model.Notes;
                vm.TotalAmount = model.TotalAmount; vm.PaidAmount = model.PaidAmount;
                vm.StartDate = model.StartDate; vm.EndDate = model.EndDate; vm.Status = model.Status;
                vm.SigningDate = model.SigningDate; vm.RenewalDate = model.RenewalDate; vm.ExpirationDate = model.ExpirationDate;
                vm.ResponsibleUserId = model.ResponsibleUserId; vm.ContractTemplateId = model.ContractTemplateId;
                vm.CustomerId = model.CustomerId; vm.ContractType = model.ContractType;
                return View(vm);
            }
            catch (KeyNotFoundException) { return NotFound(); }
        }
        try
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            await _contractService.UpdateContractAsync(model, currentUserId, ct);
            TempData["SuccessMessage"] = "قرارداد با موفقیت ویرایش شد.";
            return RedirectToAction(nameof(Details), new { id = model.Id });
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError("ContractNumber", ex.Message);
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
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
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
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
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
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
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var vm = await _contractService.GetArchiveViewModelAsync(filter, currentUserId, ct);
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize("Contract.Archive")]
    public async Task<IActionResult> ArchiveContract(int id, CancellationToken ct)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
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
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
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
            await _contractService.AddPaymentAsync(model, ct);
            TempData["SuccessMessage"] = "پرداخت با موفقیت ثبت شد.";
        }
        catch (KeyNotFoundException) { return NotFound(); }
        return RedirectToAction(nameof(Details), new { id = model.ContractId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize("Contract.Payment")]
    public async Task<IActionResult> DeletePayment(int paymentId, int contractId, CancellationToken ct)
    {
        var deleted = await _contractService.DeletePaymentAsync(paymentId, contractId, ct);
        if (!deleted) return NotFound();
        TempData["SuccessMessage"] = "پرداخت با موفقیت حذف شد.";
        return RedirectToAction(nameof(Details), new { id = contractId });
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
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
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
        var attachment = await _contractService.GetAttachmentAsync(attachmentId, ct);
        if (attachment == null) return NotFound();

        var fullPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", attachment.RelativePath.TrimStart('/'));
        if (!System.IO.File.Exists(fullPath)) return NotFound();

        return PhysicalFile(fullPath, attachment.ContentType ?? "application/octet-stream", attachment.FileName);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize("Contract.Edit")]
    public async Task<IActionResult> DeleteAttachment(int attachmentId, int contractId, CancellationToken ct)
    {
        var deleted = await _contractService.DeleteAttachmentAsync(attachmentId, contractId, ct);
        if (!deleted) return NotFound();
        TempData["SuccessMessage"] = "فایل پیوست حذف شد.";
        return RedirectToAction(nameof(Details), new { id = contractId });
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
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
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
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
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
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var vm = await _contractService.GetSettingsViewModelAsync(currentUserId, ct);
            vm.DefaultStatus = model.DefaultStatus;
            vm.DefaultResponsibleUserId = model.DefaultResponsibleUserId;
            vm.NotifyOnCreate = model.NotifyOnCreate;
            return View(vm);
        }

        var currentUser = User.FindFirstValue(ClaimTypes.NameIdentifier);
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
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var vm = await _contractService.GetSettingsViewModelAsync(currentUserId, ct);
            vm.TemplateName = model.TemplateName; vm.FormatJson = model.FormatJson;
            vm.TemplateHtml = model.TemplateHtml; vm.IsDefault = model.IsDefault;
            return View(nameof(Settings), vm);
        }
        var currentUser = User.FindFirstValue(ClaimTypes.NameIdentifier);
        await _contractService.SaveTemplateAsync(model, currentUser, ct);
        TempData["SuccessMessage"] = "قالب با موفقیت ذخیره شد.";
        return RedirectToAction(nameof(Settings));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [PermissionAuthorize("Contract.Settings")]
    public async Task<IActionResult> DeleteTemplate(int templateId, CancellationToken ct)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
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
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var set = await _contractService.SetDefaultTemplateAsync(templateId, currentUserId, ct);
        if (!set) return NotFound();
        TempData["SuccessMessage"] = "قالب پیش‌فرض با موفقیت تنظیم شد.";
        return RedirectToAction(nameof(Settings));
    }
}