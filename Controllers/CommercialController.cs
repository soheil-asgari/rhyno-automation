using OfficeAutomation.Services.Licensing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Filters;
using OfficeAutomation.Models;
using OfficeAutomation.Modules.Commercial.Application;
using OfficeAutomation.Modules.Commercial.Domain;
using OfficeAutomation.Modules.Finance.Infrastructure.Persistence;

namespace OfficeAutomation.Controllers;

[Authorize]
[RequireAccessArea("Finance")]
[RequiresModule(LicensableModules.Financial)]
public class CommercialController : Controller
{
    private readonly CommercialService _commercialService;
    private readonly FinanceDbContext _financeContext;

    public CommercialController(
        CommercialService commercialService,
        FinanceDbContext financeContext)
    {
        _commercialService = commercialService;
        _financeContext = financeContext;
    }

    // ── Sales / Purchases master grids ────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Sales(
        string? search, int? year, string? party, string? status, string? paymentStatus,
        CancellationToken cancellationToken)
    {
        var model = await _commercialService.BuildInvoiceIndexAsync("Sale", search, year, party, status, paymentStatus, cancellationToken);
        ViewBag.PageTitle = "مدیریت فروش";
        ViewBag.PageDescription = "فاکتورهای فروش، دریافتی‌ها و مانده حساب مشتریان";
        ViewBag.CreateAction = "CreateInvoice";
        ViewBag.CreateLabel = "فاکتور فروش جدید";
        ViewBag.ReturnType = "Sale";
        return View("InvoiceGrid", model);
    }

    [HttpGet]
    public async Task<IActionResult> Purchases(
        string? search, int? year, string? party, string? status, string? paymentStatus,
        CancellationToken cancellationToken)
    {
        var model = await _commercialService.BuildInvoiceIndexAsync("Purchase", search, year, party, status, paymentStatus, cancellationToken);
        ViewBag.PageTitle = "مدیریت خرید";
        ViewBag.PageDescription = "فاکتورهای خرید، پرداخت‌ها و مانده حساب تامین‌کنندگان";
        ViewBag.CreateAction = "CreateInvoice";
        ViewBag.CreateLabel = "فاکتور خرید جدید";
        ViewBag.ReturnType = "Purchase";
        return View("InvoiceGrid", model);
    }

    // ── Invoice detail (opens in a new Rhyno tab) ────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var model = await _commercialService.BuildInvoiceDetailAsync(id, cancellationToken);
        if (model == null)
        {
            return NotFound();
        }

        return View(model);
    }

    // ── Post / cancel ─────────────────────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PostInvoice(int id, string? returnTo, CancellationToken cancellationToken)
    {
        try
        {
            await _commercialService.PostInvoiceAsync(id, cancellationToken);
            TempData["SuccessMessage"] = "فاکتور ثبت نهایی (حسابداری) شد.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToAction(returnTo == "Purchase" ? nameof(Purchases) : nameof(Sales));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelInvoice(int id, string? reason, string? returnTo, CancellationToken cancellationToken)
    {
        try
        {
            await _commercialService.CancelInvoiceAsync(id, reason, cancellationToken);
            TempData["SuccessMessage"] = "فاکتور ابطال شد و سند حسابداری برگشتی صادر شد.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToAction(returnTo == "Purchase" ? nameof(Purchases) : nameof(Sales));
    }

    // ── Returns ───────────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> SalesReturns(string? search, CancellationToken cancellationToken)
    {
        var model = await _commercialService.BuildReturnIndexAsync("Sale", search, cancellationToken);
        ViewBag.PageTitle = "برگشت از فروش";
        ViewBag.PageDescription = "برگشت از فروش و ابطال بخشی از مانده فاکتور فروش";
        ViewBag.ReturnType = "Sale";
        return View("ReturnsGrid", model);
    }

    [HttpGet]
    public async Task<IActionResult> PurchaseReturns(string? search, CancellationToken cancellationToken)
    {
        var model = await _commercialService.BuildReturnIndexAsync("Purchase", search, cancellationToken);
        ViewBag.PageTitle = "برگشت از خرید";
        ViewBag.PageDescription = "برگشت از خرید و ابطال بخشی از مانده فاکتور خرید";
        ViewBag.ReturnType = "Purchase";
        return View("ReturnsGrid", model);
    }

    [HttpGet]
    public async Task<IActionResult> CreateReturn(string? type, CancellationToken cancellationToken)
    {
        var returnType = type == "Purchase" ? "Purchase" : "Sale";
        var invoices = await GetInvoicesForReturnAsync(returnType, cancellationToken);
        var options = invoices.Select(inv => new InvoiceSelectionOption(inv.Id, inv.Text)).ToList();
        ViewBag.InvoiceOptions = options;
        ViewBag.ReturnType = returnType;
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> GetInvoiceLines(int id, CancellationToken cancellationToken)
    {
        var invoice = await _financeContext.Invoices.AsNoTracking()
            .Include(item => item.Items)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (invoice == null)
        {
            return NotFound(new { success = false, message = "فاکتور یافت نشد." });
        }

        return Json(new
        {
            success = true,
            invoiceId = invoice.Id,
            invoiceNumber = invoice.InvoiceNumber,
            partyName = invoice.PartyName,
            items = invoice.Items.Select(item => new
            {
                productId = item.ProductId,
                itemName = item.ItemName,
                quantity = item.Quantity,
                unitPrice = item.UnitPrice,
                lineSubTotal = item.LineSubTotal,
                lineVatAmount = item.LineVatAmount,
                lineGrandTotal = item.LineGrandTotal
            }).ToList()
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateReturn(
        int invoiceId,
        string? reason,
        string? dateShamsi,
        DateTime? returnDate,
        List<ReturnLineInput> lines,
        CancellationToken cancellationToken)
    {
        try
        {
            var items = (lines ?? new List<ReturnLineInput>()).Select(line => new InvoiceReturnItem
            {
                ProductId = line.ProductId,
                ItemName = line.ItemName,
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice,
                LineSubTotal = line.LineSubTotal,
                LineVatAmount = line.LineVatAmount,
                LineGrandTotal = line.LineGrandTotal
            }).ToList();

            var invoice = await _financeContext.Invoices.AsNoTracking().FirstOrDefaultAsync(item => item.Id == invoiceId, cancellationToken)
                ?? throw new InvalidOperationException("فاکتور یافت نشد.");

            var currentUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            await _commercialService.CreateReturnAsync(
                invoiceId,
                reason,
                dateShamsi ?? string.Empty,
                returnDate ?? DateTime.Today,
                items,
                currentUserId,
                cancellationToken);

            TempData["SuccessMessage"] = "برگشت ثبت شد.";
            return RedirectToAction(invoice.InvoiceType == "Purchase" ? nameof(PurchaseReturns) : nameof(SalesReturns));
        }
        catch (InvalidOperationException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
            return RedirectToAction(nameof(CreateReturn), new { type = "Sale" });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> FinalizeReturn(int id, string? returnTo, CancellationToken cancellationToken)
    {
        try
        {
            await _commercialService.FinalizeReturnAsync(id, cancellationToken);
            TempData["SuccessMessage"] = "برگشت نهایی شد و سند حسابداری برگشتی صادر شد.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToAction(returnTo == "Purchase" ? nameof(PurchaseReturns) : nameof(SalesReturns));
    }

    // ── Quotations ────────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Quotations(string? type, string? search, CancellationToken cancellationToken)
    {
        var quotationType = type == "Purchase" ? "Purchase" : "Sale";
        var model = await _commercialService.BuildQuotationIndexAsync(quotationType, search, cancellationToken);
        ViewBag.PageTitle = quotationType == "Purchase" ? "پیش‌فاکتور خرید" : "پیش‌فاکتور فروش";
        ViewBag.PageDescription = "پیش‌فاکتورها و تبدیل آن‌ها به فاکتور";
        ViewBag.QuotationType = quotationType;
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> CreateQuotation(string? type, CancellationToken cancellationToken)
    {
        var quotationType = type == "Purchase" ? "Purchase" : "Sale";
        ViewBag.QuotationType = quotationType;
        ViewBag.PartyOptions = await GetPartyOptionsAsync(quotationType, cancellationToken);
        ViewBag.QuotationNumber = await _commercialService.BuildNextQuotationNumberAsync(quotationType, cancellationToken);
        ViewBag.TodayShamsi = CommercialService.ToShamsi(DateTime.Today);
        return View("QuotationEditor");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveQuotation(QuotationInput model, CancellationToken cancellationToken)
    {
        try
        {
            var entity = new CommercialQuotation
            {
                Id = model.Id ?? 0,
                QuotationType = model.QuotationType,
                QuotationNumber = model.QuotationNumber,
                CommercialPartyId = model.CommercialPartyId,
                PartyName = model.PartyName,
                NationalCodeOrEconomicId = model.NationalCodeOrEconomicId,
                QuotationDate = model.QuotationDate ?? DateTime.Today,
                DateShamsi = model.DateShamsi,
                ValidUntil = model.ValidUntil,
                ValidUntilShamsi = model.ValidUntilShamsi,
                DiscountAmount = model.DiscountAmount,
                Notes = model.Notes,
                Status = model.Status
            };

            var lines = (model.Lines ?? new List<QuotationLineInput>()).Select(line => new CommercialQuotationLine
            {
                ProductId = line.ProductId,
                ItemName = line.ItemName,
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice,
                DiscountRate = line.DiscountRate,
                DiscountAmount = line.DiscountAmount,
                LineSubTotal = line.LineSubTotal,
                LineVatAmount = line.LineVatAmount,
                LineGrandTotal = line.LineGrandTotal
            }).ToList();

            var currentUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var saved = await _commercialService.SaveQuotationAsync(entity, lines, currentUserId, cancellationToken);

            TempData["SuccessMessage"] = "پیش‌فاکتور ذخیره شد.";
            return RedirectToAction(nameof(Quotations), new { type = saved.QuotationType });
        }
        catch (InvalidOperationException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
            return RedirectToAction(nameof(CreateQuotation), new { type = model.QuotationType });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConvertQuotation(int id, string? type, CancellationToken cancellationToken)
    {
        try
        {
            await _commercialService.ConvertQuotationToInvoiceAsync(id, type, string.Empty, DateTime.Today, null, cancellationToken);
            TempData["SuccessMessage"] = "پیش‌فاکتور به فاکتور تبدیل شد.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToAction(nameof(Quotations), new { type });
    }

    // ── Parties ───────────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Parties(string? search, CancellationToken cancellationToken)
    {
        var items = await _commercialService.GetPartiesAsync(search, cancellationToken);
        ViewBag.Search = search;
        return View(items);
    }

    [HttpGet]
    public IActionResult CreateParty() => View("PartyEditor", new PartyUpsertVM { IsActive = true });

    [HttpGet]
    public async Task<IActionResult> EditParty(int id, CancellationToken cancellationToken)
    {
        var model = await _commercialService.GetPartyForEditAsync(id, cancellationToken);
        if (model == null)
        {
            return NotFound();
        }

        return View("PartyEditor", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveParty(PartyUpsertVM model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid || string.IsNullOrWhiteSpace(model.Name))
        {
            if (string.IsNullOrWhiteSpace(model.Name))
            {
                ModelState.AddModelError(nameof(model.Name), "نام طرف حساب الزامی است.");
            }
            return View("PartyEditor", model);
        }

        var currentUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        await _commercialService.SavePartyAsync(model, currentUserId, cancellationToken);
        TempData["SuccessMessage"] = "طرف حساب ذخیره شد.";
        return RedirectToAction(nameof(Parties));
    }

    // ── Settlements ───────────────────────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegisterSettlement(
        int invoiceId,
        decimal amount,
        string? referenceNumber,
        string? paymentMethod,
        string? dateShamsi,
        DateTime? settlementDate,
        string? notes,
        bool postNow,
        string? returnTo,
        CancellationToken cancellationToken)
    {
        try
        {
            var currentUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            await _commercialService.RegisterSettlementAsync(
                invoiceId,
                amount,
                referenceNumber,
                paymentMethod,
                dateShamsi ?? string.Empty,
                settlementDate ?? DateTime.Today,
                notes,
                currentUserId,
                postNow,
                cancellationToken);
            TempData["SuccessMessage"] = "تسویه ثبت شد و مانده فاکتور به‌روزرسانی شد.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToAction(nameof(Details), new { id = invoiceId, _tab = true, returnTo });
    }

    // ── Attachments ───────────────────────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadAttachment(
        int invoiceId,
        IFormFile file,
        InvoiceAttachmentCategory category,
        string? description,
        string? returnTo,
        CancellationToken cancellationToken)
    {
        try
        {
            if (file == null || file.Length == 0)
            {
                throw new InvalidOperationException("فایلی انتخاب نشده است.");
            }

            var uploadsRoot = Path.Combine(Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot")), "uploads", "commercial", "invoices");
            Directory.CreateDirectory(uploadsRoot);

            var safeFileName = Path.GetFileName(file.FileName);
            var storedName = $"{Guid.NewGuid():N}{Path.GetExtension(safeFileName)}";
            var storedPath = Path.Combine(uploadsRoot, storedName);

            await using (var stream = System.IO.File.Create(storedPath))
            {
                await file.CopyToAsync(stream, cancellationToken);
            }

            var currentUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var relativePath = $"uploads/commercial/invoices/{storedName}";
            await _commercialService.AddAttachmentAsync(
                invoiceId,
                safeFileName,
                relativePath,
                file.ContentType,
                file.Length,
                category,
                description,
                currentUserId,
                cancellationToken);

            TempData["SuccessMessage"] = "پیوست ثبت شد.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["ErrorMessage"] = ex.Message;
        }

        return RedirectToAction(nameof(Details), new { id = invoiceId, _tab = true, returnTo });
    }

    [HttpGet]
    public async Task<IActionResult> DownloadAttachment(int id, CancellationToken cancellationToken)
    {
        var attachment = await _commercialService.GetAttachmentAsync(id, cancellationToken);
        if (attachment == null)
        {
            return NotFound();
        }

        var fullPath = Path.Combine(Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot")), attachment.StoredPath);
        if (!System.IO.File.Exists(fullPath))
        {
            return NotFound();
        }

        return PhysicalFile(fullPath, attachment.ContentType ?? "application/octet-stream", attachment.FileName);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAttachment(int id, int invoiceId, CancellationToken cancellationToken)
    {
        await _commercialService.DeleteAttachmentAsync(id, cancellationToken);
        TempData["SuccessMessage"] = "پیوست حذف شد.";
        return RedirectToAction(nameof(Details), new { id = invoiceId, _tab = true });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<List<InvoiceSelectionRecord>> GetInvoicesForReturnAsync(string returnType, CancellationToken cancellationToken)
    {
        return await _financeContext.Invoices.AsNoTracking()
            .Where(item => item.InvoiceType == returnType)
            .OrderByDescending(item => item.InvoiceDate)
            .Select(item => new InvoiceSelectionRecord(item.Id, $"{item.InvoiceNumber} - {item.PartyName}"))
            .ToListAsync(cancellationToken);
    }

    private async Task<List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem>> GetPartyOptionsAsync(string type, CancellationToken cancellationToken)
    {
        var targetType = type == "Purchase" ? CommercialPartyType.Supplier : CommercialPartyType.Customer;
        var items = await _financeContext.CommercialParties.AsNoTracking()
            .Where(item => item.IsActive &&
                           (item.PartyType == targetType || item.PartyType == CommercialPartyType.Both))
            .OrderBy(item => item.Name)
            .Select(item => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
            {
                Value = item.Id.ToString(),
                Text = item.Name + (string.IsNullOrWhiteSpace(item.EconomicCode) ? "" : $" ({item.EconomicCode})")
            })
            .ToListAsync(cancellationToken);
        return items;
    }
}

public sealed record InvoiceSelectionRecord(int Id, string Text);

public sealed record InvoiceSelectionOption(int Value, string Text);

public sealed class ReturnLineInput
{
    public int? ProductId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineSubTotal { get; set; }
    public decimal LineVatAmount { get; set; }
    public decimal LineGrandTotal { get; set; }
}

public sealed class QuotationInput
{
    public int? Id { get; set; }
    public string QuotationType { get; set; } = "Sale";
    public string QuotationNumber { get; set; } = string.Empty;
    public int? CommercialPartyId { get; set; }
    public string PartyName { get; set; } = string.Empty;
    public string? NationalCodeOrEconomicId { get; set; }
    public DateTime? QuotationDate { get; set; }
    public string DateShamsi { get; set; } = string.Empty;
    public DateTime? ValidUntil { get; set; }
    public string? ValidUntilShamsi { get; set; }
    public decimal DiscountAmount { get; set; }
    public string? Notes { get; set; }
    public string Status { get; set; } = CommercialQuotationStatus.Draft;
    public List<QuotationLineInput> Lines { get; set; } = new();
}

public sealed class QuotationLineInput
{
    public int? ProductId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal DiscountRate { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal LineSubTotal { get; set; }
    public decimal LineVatAmount { get; set; }
    public decimal LineGrandTotal { get; set; }
}