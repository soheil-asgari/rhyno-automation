using System.Globalization;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Filters;
using OfficeAutomation.Models;
using OfficeAutomation.Modules.Finance.Application;
using OfficeAutomation.Modules.Finance.Domain;
using OfficeAutomation.Modules.Finance.Infrastructure.Persistence;
using OfficeAutomation.Modules.Platform.Application.SavedViews;
using OfficeAutomation.Modules.Inventory.Infrastructure.Persistence;
using OfficeAutomation.Modules.Office.Infrastructure.Persistence;
using OfficeAutomation.Services;
using OfficeAutomation.Services.Security;
using OfficeAutomation.Utilities;

namespace OfficeAutomation.Controllers
{
    [Authorize]
    [RequireAccessArea("Finance")]
    public class FinancialController : Controller
    {
        private readonly FinanceDbContext _context;
        private readonly InventoryDbContext _inventoryContext;
        private readonly OfficeDbContext _officeContext;
        private readonly FinancialInvoiceService _invoiceService;
        private readonly FinanceLedgerService _ledgerService;
        private readonly PeriodClosingService _periodClosingService;
        private readonly TrialBalanceService _trialBalanceService;
        private readonly VoucherRenumberingService _voucherRenumberingService;
        private readonly WorkflowService _workflowService;
        private readonly ISegregationOfDutiesService _segregationOfDutiesService;
        private readonly IAbacAuthorizationService _abacAuthorizationService;
        private readonly ICurrentUserContextAccessor _currentUserContextAccessor;
        private readonly ISecurityFieldMaskingService _securityFieldMaskingService;
        private readonly ITableSchemaRegistry _tableSchemaRegistry;

        public FinancialController(
            FinanceDbContext context,
            InventoryDbContext inventoryContext,
            OfficeDbContext officeContext,
            FinancialInvoiceService invoiceService,
            FinanceLedgerService ledgerService,
            PeriodClosingService periodClosingService,
            TrialBalanceService trialBalanceService,
            VoucherRenumberingService voucherRenumberingService,
            WorkflowService workflowService,
            ISegregationOfDutiesService segregationOfDutiesService,
            IAbacAuthorizationService abacAuthorizationService,
            ICurrentUserContextAccessor currentUserContextAccessor,
            ISecurityFieldMaskingService securityFieldMaskingService,
            ITableSchemaRegistry tableSchemaRegistry)
        {
            _context = context;
            _inventoryContext = inventoryContext;
            _officeContext = officeContext;
            _invoiceService = invoiceService;
            _ledgerService = ledgerService;
            _periodClosingService = periodClosingService;
            _trialBalanceService = trialBalanceService;
            _voucherRenumberingService = voucherRenumberingService;
            _workflowService = workflowService;
            _segregationOfDutiesService = segregationOfDutiesService;
            _abacAuthorizationService = abacAuthorizationService;
            _currentUserContextAccessor = currentUserContextAccessor;
            _securityFieldMaskingService = securityFieldMaskingService;
            _tableSchemaRegistry = tableSchemaRegistry;
        }

        [HttpGet]
        public Task<IActionResult> Invoices(FinancialInvoiceIndexVM filter, string? invoiceType, CancellationToken cancellationToken)
        {
            if (string.Equals(invoiceType, "Purchase", StringComparison.OrdinalIgnoreCase))
            {
                return Purchases(filter, cancellationToken);
            }

            return Sales(filter, cancellationToken);
        }

        [HttpGet]
        [PermissionAuthorize("Finance.View")]
        public async Task<IActionResult> Sales(FinancialInvoiceIndexVM filter, CancellationToken cancellationToken)
        {
            var model = await BuildInvoiceIndexAsync(filter, "Sale", cancellationToken);
            await _securityFieldMaskingService.MaskInvoicesAsync(model, cancellationToken);
            ViewData["Title"] = "مدیریت فاکتورهای فروش";
            ViewBag.PageTitle = "مدیریت فاکتورهای فروش (درآمد)";
            ViewBag.PageDescription = "مدیریت فرآیند فروش، ثبت اسناد و پیگیری درآمد";
            ViewBag.CreateInvoiceType = "Sale";
            ViewBag.ExportInvoiceType = "Sale";
            ViewBag.BackToListAction = nameof(Sales);
            return View("Invoices", model);
        }

        [HttpGet]
        [PermissionAuthorize("Finance.View")]
        public async Task<IActionResult> Purchases(FinancialInvoiceIndexVM filter, CancellationToken cancellationToken)
        {
            var model = await BuildInvoiceIndexAsync(filter, "Purchase", cancellationToken);
            await _securityFieldMaskingService.MaskInvoicesAsync(model, cancellationToken);
            ViewData["Title"] = "مدیریت فاکتورهای خرید";
            ViewBag.PageTitle = "مدیریت فاکتورهای خرید (هزینه)";
            ViewBag.PageDescription = "مدیریت هزینه‌ها و اتصال اسناد خرید به رسید انبار";
            ViewBag.CreateInvoiceType = "Purchase";
            ViewBag.ExportInvoiceType = "Purchase";
            ViewBag.BackToListAction = nameof(Purchases);
            return View("Invoices", model);
        }

        [HttpGet]
        [PermissionAuthorize("Finance.Create")]
        public async Task<IActionResult> CreateInvoice(string? invoiceType, CancellationToken cancellationToken)
        {
            var normalizedType = invoiceType == "Purchase" ? "Purchase" : "Sale";
            var model = new FinancialInvoiceUpsertVM
            {
                DateShamsi = GetTodayShamsi(),
                InvoiceType = normalizedType,
                InvoiceNumber = await BuildNextInvoiceNumberAsync(cancellationToken),
                Items = new List<FinancialInvoiceItemVM> { new() }
            };

            await PopulateProductOptionsAsync(model.ProductOptions, cancellationToken);
            await PopulatePurchaseOptionsAsync(model, cancellationToken);
            await PopulateEmployerOptionsAsync(model.EmployerOptions, cancellationToken);
            ViewBag.BackToListAction = normalizedType == "Purchase" ? nameof(Purchases) : nameof(Sales);
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> CheckInvoiceNumber(string? invoiceNumber, string? invoiceType, int? currentInvoiceId, CancellationToken cancellationToken)
        {
            var normalizedNumber = string.IsNullOrWhiteSpace(invoiceNumber) ? string.Empty : PersianTextNormalizer.Normalize(invoiceNumber);
            if (string.IsNullOrWhiteSpace(normalizedNumber))
            {
                return Json(new { isDuplicate = false, message = string.Empty });
            }

            var duplicate = await _invoiceService.IsDuplicateInvoiceNumberAsync(
                normalizedNumber,
                invoiceType,
                currentInvoiceId,
                cancellationToken);

            return Json(new
            {
                isDuplicate = duplicate,
                message = duplicate ? "این شماره فاکتور برای نوع انتخابی تکراری است." : "شماره فاکتور قابل استفاده است."
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetReceiptItemsJson(int receiptId, CancellationToken cancellationToken)
        {
            var receipt = await _inventoryContext.WarehouseReceipts
                .AsNoTracking()
                .Include(item => item.Items)
                .ThenInclude(item => item.Product)
                .FirstOrDefaultAsync(item => item.Id == receiptId, cancellationToken);

            if (receipt == null)
            {
                return NotFound(new { success = false, message = "رسید انبار یافت نشد." });
            }

            var items = receipt.Items
                .Where(item => item.ProductId > 0 && item.Quantity > 0)
                .Select(item => new
                {
                    productId = item.ProductId,
                    itemName = item.Product?.Name ?? $"کالا {item.ProductId}",
                    quantity = item.Quantity,
                    unitPrice = item.UnitPrice,
                    lineSubTotal = Math.Round(item.Quantity * item.UnitPrice, 2),
                    lineVatAmount = Math.Round(item.Quantity * item.UnitPrice * 0.10m, 2),
                    lineGrandTotal = Math.Round(item.Quantity * item.UnitPrice * 1.10m, 2)
                })
                .ToList();

            return Json(new
            {
                success = true,
                receiptId = receipt.Id,
                receiptNumber = receipt.ReceiptNumber,
                items
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Finance.Create")]
        public async Task<IActionResult> CreateInvoice(FinancialInvoiceUpsertVM model, CancellationToken cancellationToken)
        {
            await ValidateInvoiceAsync(model, cancellationToken);

            if (!ModelState.IsValid)
            {
                await PopulateProductOptionsAsync(model.ProductOptions, cancellationToken);
                await PopulatePurchaseOptionsAsync(model, cancellationToken);
                await PopulateEmployerOptionsAsync(model.EmployerOptions, cancellationToken);
                ViewBag.BackToListAction = model.InvoiceType == "Purchase" ? nameof(Purchases) : nameof(Sales);
                return View(model);
            }

            var calculation = _invoiceService.CalculateTotals(model.Items);
            var validItems = calculation.ValidItems;
            model.SubTotal = calculation.SubTotal;
            model.VatAmount = calculation.VatAmount;
            model.GrandTotal = calculation.GrandTotal;

            var invoiceDate = TryParseShamsiDate(model.DateShamsi, out var parsedDate)
                ? parsedDate
                : DateTime.Now;
            var resolvedPartyName = model.InvoiceType == "Sale"
                ? await ResolveEmployerNameAsync(model.EmployerId, model.PartyName, cancellationToken)
                : (model.PartyName?.Trim() ?? string.Empty);

            var entity = new Invoice
            {
                InvoiceNumber = model.InvoiceNumber.Trim(),
                InvoiceType = model.InvoiceType,
                DateShamsi = PersianTextNormalizer.Normalize(model.DateShamsi),
                PartyName = resolvedPartyName,
                NationalCodeOrEconomicId = model.NationalCodeOrEconomicId?.Trim(),
                SubTotal = model.SubTotal,
                VatAmount = model.VatAmount,
                GrandTotal = model.GrandTotal,
                Notes = model.Notes?.Trim(),
                WorkflowStatus = WorkflowStatus.Sent,
                EmployerId = model.InvoiceType == "Sale" ? model.EmployerId : null,
                WarehouseReceiptId = model.InvoiceType == "Purchase" ? model.WarehouseReceiptId : null,
                FollowUpEmployeeId = model.InvoiceType == "Purchase" ? model.FollowUpEmployeeId : null,
                DeadlineDateShamsi = model.InvoiceType == "Purchase" ? NormalizeOptionalShamsi(model.DeadlineDateShamsi) : null,
                Amount = model.GrandTotal,
                VendorName = resolvedPartyName,
                InvoiceDate = invoiceDate,
                CreatedAt = DateTime.Now,
                CreatedByUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
                Items = validItems.Select(item => new InvoiceItem
                {
                    ProductId = item.ProductId,
                    ItemName = item.ItemName.Trim(),
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    LineSubTotal = item.LineSubTotal,
                    LineVatAmount = item.LineVatAmount,
                    LineGrandTotal = item.LineGrandTotal
                }).ToList()
            };

            _context.Invoices.Add(entity);
            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                ModelState.AddModelError(nameof(model.InvoiceNumber), "شماره فاکتور برای نوع انتخابی قبلاً ثبت شده است.");
                await PopulateProductOptionsAsync(model.ProductOptions, cancellationToken);
                await PopulatePurchaseOptionsAsync(model, cancellationToken);
                ViewBag.BackToListAction = model.InvoiceType == "Purchase" ? nameof(Purchases) : nameof(Sales);
                return View(model);
            }

            await _workflowService.StartRoutingAsync(
                "Invoice",
                User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty,
                User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty,
                entity.Id);

            var targetAction = model.InvoiceType == "Purchase" ? nameof(Purchases) : nameof(Sales);
            return RedirectToAction(targetAction);
        }

        [HttpGet]
        [PermissionAuthorize("Finance.Edit")]
        public async Task<IActionResult> EditInvoice(int id, CancellationToken cancellationToken)
        {
            var entity = await _context.Invoices
                .AsNoTracking()
                .Include(item => item.Items)
                .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

            if (entity == null)
            {
                return NotFound();
            }

            var model = new FinancialInvoiceUpsertVM
            {
                Id = entity.Id,
                InvoiceNumber = entity.InvoiceNumber,
                InvoiceType = entity.InvoiceType,
                DateShamsi = entity.DateShamsi,
                PartyName = entity.PartyName,
                NationalCodeOrEconomicId = entity.NationalCodeOrEconomicId,
                SubTotal = entity.SubTotal,
                VatAmount = entity.VatAmount,
                GrandTotal = entity.GrandTotal,
                Notes = entity.Notes,
                WarehouseReceiptId = entity.WarehouseReceiptId,
                FollowUpEmployeeId = entity.FollowUpEmployeeId,
                EmployerId = entity.EmployerId,
                DeadlineDateShamsi = entity.DeadlineDateShamsi,
                Items = entity.Items
                    .OrderBy(item => item.Id)
                    .Select(item => new FinancialInvoiceItemVM
                    {
                        ProductId = item.ProductId,
                        ItemName = item.ItemName,
                        Quantity = item.Quantity,
                        UnitPrice = item.UnitPrice,
                        LineSubTotal = item.LineSubTotal,
                        LineVatAmount = item.LineVatAmount,
                        LineGrandTotal = item.LineGrandTotal
                    })
                    .ToList()
            };

            await PopulateProductOptionsAsync(model.ProductOptions, cancellationToken);
            await PopulatePurchaseOptionsAsync(model, cancellationToken);
            await PopulateEmployerOptionsAsync(model.EmployerOptions, cancellationToken);
            ViewBag.BackToListAction = model.InvoiceType == "Purchase" ? nameof(Purchases) : nameof(Sales);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Finance.Edit")]
        public async Task<IActionResult> EditInvoice(int id, FinancialInvoiceUpsertVM model, CancellationToken cancellationToken)
        {
            if (id != model.Id)
            {
                return NotFound();
            }

            await ValidateInvoiceAsync(model, cancellationToken, id);

            if (!ModelState.IsValid)
            {
                await PopulateProductOptionsAsync(model.ProductOptions, cancellationToken);
                await PopulatePurchaseOptionsAsync(model, cancellationToken);
                await PopulateEmployerOptionsAsync(model.EmployerOptions, cancellationToken);
                ViewBag.BackToListAction = model.InvoiceType == "Purchase" ? nameof(Purchases) : nameof(Sales);
                return View(model);
            }

            var entity = await _context.Invoices
                .Include(item => item.Items)
                .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

            if (entity == null)
            {
                return NotFound();
            }

            var calculation = _invoiceService.CalculateTotals(model.Items);
            var validItems = calculation.ValidItems;
            model.SubTotal = calculation.SubTotal;
            model.VatAmount = calculation.VatAmount;
            model.GrandTotal = calculation.GrandTotal;

            var invoiceDate = TryParseShamsiDate(model.DateShamsi, out var parsedDate)
                ? parsedDate
                : entity.InvoiceDate;
            var resolvedPartyName = model.InvoiceType == "Sale"
                ? await ResolveEmployerNameAsync(model.EmployerId, model.PartyName, cancellationToken)
                : (model.PartyName?.Trim() ?? string.Empty);

            var beforeState = new
            {
                entity.InvoiceNumber,
                entity.InvoiceType,
                entity.DateShamsi,
                entity.PartyName,
                entity.SubTotal,
                entity.VatAmount,
                entity.GrandTotal,
                entity.WarehouseReceiptId,
                entity.FollowUpEmployeeId,
                entity.EmployerId,
                entity.DeadlineDateShamsi,
                ItemCount = entity.Items.Count
            };

            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                entity.InvoiceNumber = model.InvoiceNumber.Trim();
                entity.InvoiceType = model.InvoiceType;
                entity.DateShamsi = PersianTextNormalizer.Normalize(model.DateShamsi);
                entity.PartyName = resolvedPartyName;
                entity.NationalCodeOrEconomicId = model.NationalCodeOrEconomicId?.Trim();
                entity.SubTotal = model.SubTotal;
                entity.VatAmount = model.VatAmount;
                entity.GrandTotal = model.GrandTotal;
                entity.Notes = model.Notes?.Trim();
                entity.WorkflowStatus = WorkflowStatus.Normalize(entity.WorkflowStatus);
                entity.EmployerId = model.InvoiceType == "Sale" ? model.EmployerId : null;
                entity.WarehouseReceiptId = model.InvoiceType == "Purchase" ? model.WarehouseReceiptId : null;
                entity.FollowUpEmployeeId = model.InvoiceType == "Purchase" ? model.FollowUpEmployeeId : null;
                entity.DeadlineDateShamsi = model.InvoiceType == "Purchase" ? NormalizeOptionalShamsi(model.DeadlineDateShamsi) : null;
                entity.Amount = model.GrandTotal;
                entity.VendorName = resolvedPartyName;
                entity.InvoiceDate = invoiceDate;

                if (entity.Items.Count > 0)
                {
                    _context.InvoiceItems.RemoveRange(entity.Items);
                }

                entity.Items = validItems.Select(item => new InvoiceItem
                {
                    ProductId = item.ProductId,
                    ItemName = item.ItemName.Trim(),
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    LineSubTotal = item.LineSubTotal,
                    LineVatAmount = item.LineVatAmount,
                    LineGrandTotal = item.LineGrandTotal
                }).ToList();

                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                await transaction.RollbackAsync(cancellationToken);
                ModelState.AddModelError(nameof(model.InvoiceNumber), "شماره فاکتور برای نوع انتخابی قبلاً ثبت شده است.");
                await PopulateProductOptionsAsync(model.ProductOptions, cancellationToken);
                await PopulatePurchaseOptionsAsync(model, cancellationToken);
                await PopulateEmployerOptionsAsync(model.EmployerOptions, cancellationToken);
                ViewBag.BackToListAction = model.InvoiceType == "Purchase" ? nameof(Purchases) : nameof(Sales);
                return View(model);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                ModelState.AddModelError(string.Empty, "ویرایش فاکتور انجام نشد. لطفاً دوباره تلاش کنید.");
                await PopulateProductOptionsAsync(model.ProductOptions, cancellationToken);
                await PopulatePurchaseOptionsAsync(model, cancellationToken);
                await PopulateEmployerOptionsAsync(model.EmployerOptions, cancellationToken);
                ViewBag.BackToListAction = model.InvoiceType == "Purchase" ? nameof(Purchases) : nameof(Sales);
                return View(model);
            }

            TempData["SuccessMessage"] = "فاکتور با موفقیت ویرایش شد.";
            var targetAction = model.InvoiceType == "Purchase" ? nameof(Purchases) : nameof(Sales);
            return RedirectToAction(targetAction);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Finance.Approve")]
        public async Task<IActionResult> ApproveInvoice(int id, string? returnTo, CancellationToken cancellationToken)
        {
            var invoice = await _context.Invoices.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (invoice == null)
            {
                return NotFound();
            }

            var currentUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var sodResult = await _segregationOfDutiesService.ValidateFinanceApprovalAsync(invoice, currentUserId, cancellationToken);
            if (!sodResult.Allowed)
            {
                TempData["ErrorMessage"] = sodResult.Reason ?? "سیاست تفکیک وظایف اجازه تایید این فاکتور را نمی‌دهد.";
                return RedirectToInvoiceList(returnTo, invoice.InvoiceType);
            }

            var profile = await _currentUserContextAccessor.GetAccessProfileAsync(cancellationToken);
            var abacResult = await _abacAuthorizationService.AuthorizeAsync(profile, new AbacResourceContext("Finance", "Approve", invoice.CreatedByUserId, null, true), cancellationToken);
            if (!abacResult.Allowed)
            {
                return Forbid();
            }

            invoice.WorkflowStatus = WorkflowStatus.Approved;
            await _context.SaveChangesAsync(cancellationToken);
            await _ledgerService.PostInvoiceAsync(invoice, cancellationToken);
            await _workflowService.RecordDecisionAsync(
                "Invoice",
                invoice.Id,
                1,
                User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty,
                WorkflowStatus.Approved,
                cancellationToken: cancellationToken);
            await _invoiceService.PublishInvoiceDecisionNotificationAsync(
                invoice,
                WorkflowStatus.Approved,
                NotificationSeverity.Success,
                cancellationToken);

            TempData["SuccessMessage"] = "فاکتور تایید شد.";
            return RedirectToInvoiceList(returnTo, invoice.InvoiceType);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Finance.Approve")]
        public async Task<IActionResult> RejectInvoice(int id, string? returnTo, CancellationToken cancellationToken)
        {
            var invoice = await _context.Invoices.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (invoice == null)
            {
                return NotFound();
            }

            var currentUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var profile = await _currentUserContextAccessor.GetAccessProfileAsync(cancellationToken);
            var abacResult = await _abacAuthorizationService.AuthorizeAsync(profile, new AbacResourceContext("Finance", "Approve", invoice.CreatedByUserId, null, true), cancellationToken);
            if (!abacResult.Allowed)
            {
                return Forbid();
            }

            invoice.WorkflowStatus = WorkflowStatus.Rejected;
            await _context.SaveChangesAsync(cancellationToken);
            await _workflowService.RecordDecisionAsync(
                "Invoice",
                invoice.Id,
                1,
                User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty,
                WorkflowStatus.Rejected,
                cancellationToken: cancellationToken);
            await _invoiceService.PublishInvoiceDecisionNotificationAsync(
                invoice,
                WorkflowStatus.Rejected,
                NotificationSeverity.Danger,
                cancellationToken);

            TempData["SuccessMessage"] = "فاکتور رد شد.";
            return RedirectToInvoiceList(returnTo, invoice.InvoiceType);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Finance.Delete")]
        public async Task<IActionResult> DeleteInvoice(int id, string? returnTo, CancellationToken cancellationToken)
        {
            var normalizedReturn = string.Equals(returnTo, "Purchase", StringComparison.OrdinalIgnoreCase)
                ? nameof(Purchases)
                : nameof(Sales);

            var entity = await _context.Invoices
                .Include(item => item.Items)
                .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

            if (entity == null)
            {
                TempData["ErrorMessage"] = "فاکتور موردنظر یافت نشد.";
                return RedirectToAction(normalizedReturn);
            }

            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var deletedSnapshot = new
                {
                    entity.InvoiceNumber,
                    entity.InvoiceType,
                    entity.DateShamsi,
                    entity.PartyName,
                    entity.SubTotal,
                    entity.VatAmount,
                    entity.GrandTotal,
                    ItemCount = entity.Items.Count
                };

                _context.Invoices.Remove(entity);
                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                TempData["ErrorMessage"] = "حذف فاکتور انجام نشد. لطفاً دوباره تلاش کنید.";
                var failedRedirect = entity.InvoiceType == "Purchase" ? nameof(Purchases) : nameof(Sales);
                return RedirectToAction(failedRedirect);
            }

            TempData["SuccessMessage"] = "فاکتور با موفقیت حذف شد.";
            var targetRedirect = entity.InvoiceType == "Purchase" ? nameof(Purchases) : nameof(Sales);
            return RedirectToAction(targetRedirect);
        }

        private IActionResult RedirectToInvoiceList(string? returnTo, string invoiceType)
        {
            var targetAction = string.Equals(returnTo, "Purchase", StringComparison.OrdinalIgnoreCase) ||
                               string.Equals(invoiceType, "Purchase", StringComparison.OrdinalIgnoreCase)
                ? nameof(Purchases)
                : nameof(Sales);

            return RedirectToAction(targetAction);
        }

        [HttpGet]
        public async Task<IActionResult> Dashboard(int? year, CancellationToken cancellationToken)
        {
            var targetYear = year ?? GetCurrentShamsiYear();

            var invoices = await _context.Invoices
                .AsNoTracking()
                .Where(item => item.DateShamsi.StartsWith(targetYear.ToString()))
                .ToListAsync(cancellationToken);

            var totalRevenue = invoices.Where(item => item.InvoiceType == "Sale").Sum(item => item.GrandTotal);
            var totalPurchaseCost = invoices.Where(item => item.InvoiceType == "Purchase").Sum(item => item.GrandTotal);
            var pendingSalesInvoices = invoices.Count(item => item.InvoiceType == "Sale" && item.WorkflowStatus != WorkflowStatus.Approved && item.WorkflowStatus != WorkflowStatus.Archived);
            var pendingPurchaseInvoices = invoices.Count(item => item.InvoiceType == "Purchase" && item.WorkflowStatus != WorkflowStatus.Approved && item.WorkflowStatus != WorkflowStatus.Archived);
            var duePurchaseDeadlines = invoices.Count(item => item.InvoiceType == "Purchase" && item.DeadlineDateShamsi != null && item.WorkflowStatus != WorkflowStatus.Approved && item.WorkflowStatus != WorkflowStatus.Archived);
            var validationWarnings = invoices.Count(item =>
                string.IsNullOrWhiteSpace(item.InvoiceNumber) ||
                string.IsNullOrWhiteSpace(item.PartyName) ||
                item.GrandTotal <= 0 ||
                item.VatAmount < 0);

            var payrollCost = await _context.PayrollLists
                .AsNoTracking()
                .Include(item => item.Items)
                .Where(item => item.Year == targetYear)
                .SelectMany(item => item.Items)
                .SumAsync(item => item.NetPayable, cancellationToken);

            var totalCosts = totalPurchaseCost + payrollCost;
            var netProfitLoss = totalRevenue - totalCosts;

            var monthLabels = Enumerable.Range(1, 12).Select(item => $"{item:00}").ToList();
            var revenueSeries = new List<decimal>();
            var costSeries = new List<decimal>();
            var profitSeries = new List<decimal>();

            var monthlyAggregates = await _context.Invoices
                .AsNoTracking()
                .Where(item =>
                    item.DateShamsi.StartsWith(targetYear.ToString()) &&
                    item.DateShamsi.Length >= 7 &&
                    (item.InvoiceType == "Sale" || item.InvoiceType == "Purchase"))
                .Select(item => new
                {
                    Month = item.DateShamsi.Substring(5, 2),
                    Revenue = item.InvoiceType == "Sale" ? item.GrandTotal : 0m,
                    Purchase = item.InvoiceType == "Purchase" ? item.GrandTotal : 0m,
                    Payroll = 0m
                })
                .Concat(
                    _context.PayrollItems
                        .AsNoTracking()
                        .Where(item => item.PayrollList.Year == targetYear)
                        .Select(item => new
                        {
                            Month = (item.PayrollList.Month < 10 ? "0" : "") + item.PayrollList.Month,
                            Revenue = 0m,
                            Purchase = 0m,
                            Payroll = item.NetPayable
                        }))
                .GroupBy(item => item.Month)
                .Select(group => new
                {
                    Month = group.Key,
                    Revenue = group.Sum(item => item.Revenue),
                    Purchase = group.Sum(item => item.Purchase),
                    Payroll = group.Sum(item => item.Payroll)
                })
                .ToDictionaryAsync(item => item.Month, item => item, cancellationToken);

            for (var month = 1; month <= 12; month++)
            {
                var monthKey = $"{month:00}";
                var monthRevenue = monthlyAggregates.TryGetValue(monthKey, out var aggregate) ? aggregate.Revenue : 0;
                var monthPurchase = monthlyAggregates.TryGetValue(monthKey, out aggregate) ? aggregate.Purchase : 0;
                var monthPayroll = monthlyAggregates.TryGetValue(monthKey, out aggregate) ? aggregate.Payroll : 0;
                var monthCost = monthPurchase + monthPayroll;

                revenueSeries.Add(Math.Round(monthRevenue, 2));
                costSeries.Add(Math.Round(monthCost, 2));
                profitSeries.Add(Math.Round(monthRevenue - monthCost, 2));
            }

            var model = new FinancialDashboardVM
            {
                PendingSalesInvoices = pendingSalesInvoices,
                PendingPurchaseInvoices = pendingPurchaseInvoices,
                DuePurchaseDeadlines = duePurchaseDeadlines,
                ValidationWarnings = validationWarnings,
                HubKpis =
                [
                    new FinancialHubKpiVM
                    {
                        Title = "فاکتورهای فروش باز",
                        Value = pendingSalesInvoices.ToString("N0"),
                        Description = "فاکتورهای ثبت شده اما هنوز نهایی نشده",
                        Tone = "success"
                    },
                    new FinancialHubKpiVM
                    {
                        Title = "فاکتورهای خرید باز",
                        Value = pendingPurchaseInvoices.ToString("N0"),
                        Description = "خریدهای در گردش یا در انتظار بررسی",
                        Tone = "primary"
                    },
                    new FinancialHubKpiVM
                    {
                        Title = "سررسیدهای نزدیک",
                        Value = duePurchaseDeadlines.ToString("N0"),
                        Description = "مواردی که باید همین حالا پیگیری شوند",
                        Tone = "warning"
                    },
                    new FinancialHubKpiVM
                    {
                        Title = "هشدارهای اعتبارسنجی",
                        Value = validationWarnings.ToString("N0"),
                        Description = "رکوردهای ناقص یا مشکوک",
                        Tone = "danger"
                    }
                ],
                QuickActions =
                [
                    new FinancialQuickActionVM { Title = "فروش", Description = "ثبت و پیگیری فاکتورهای فروش", Url = Url.Action(nameof(Sales)) ?? "#", Icon = "bi-graph-up-arrow", Tone = "success" },
                    new FinancialQuickActionVM { Title = "خرید", Description = "ثبت و پیگیری فاکتورهای خرید", Url = Url.Action(nameof(Purchases)) ?? "#", Icon = "bi-cart", Tone = "primary" },
                    new FinancialQuickActionVM { Title = "مالیات بر ارزش افزوده", Description = "کنترل مالیات بر ارزش افزوده و مغایرت‌ها", Url = Url.Action(nameof(VatDashboard)) ?? "#", Icon = "bi-percent", Tone = "warning" },
                    new FinancialQuickActionVM { Title = "معاملات فصلی", Description = "گزارش ماده 169 و خروجی‌ها", Url = Url.Action(nameof(SeasonalTax)) ?? "#", Icon = "bi-journal-text", Tone = "info" },
                    new FinancialQuickActionVM { Title = "حقوق", Description = "ورود به مدیریت حقوق و دستمزد", Url = Url.Action("Index", "Payroll") ?? "#", Icon = "bi-cash-stack", Tone = "secondary" },
                    new FinancialQuickActionVM { Title = "بیمه", Description = "ورود به مدیریت بیمه و لیست‌ها", Url = Url.Action("Index", "Bimeh") ?? "#", Icon = "bi-shield-lock", Tone = "dark" }
                ],
                TotalRevenue = Math.Round(totalRevenue, 2),
                TotalPurchaseCost = Math.Round(totalPurchaseCost, 2),
                TotalPayrollCost = Math.Round(payrollCost, 2),
                TotalCosts = Math.Round(totalCosts, 2),
                NetProfitLoss = Math.Round(netProfitLoss, 2),
                MonthLabels = monthLabels,
                RevenueSeries = revenueSeries,
                CostSeries = costSeries,
                ProfitSeries = profitSeries,
                TotalVatCollected = Math.Round(invoices.Where(item => item.InvoiceType == "Sale").Sum(item => item.VatAmount), 2),
                TotalVatPaid = Math.Round(invoices.Where(item => item.InvoiceType == "Purchase").Sum(item => item.VatAmount), 2),
                NetVatPayableOrRefundable = Math.Round(invoices.Where(item => item.InvoiceType == "Sale").Sum(item => item.VatAmount) - invoices.Where(item => item.InvoiceType == "Purchase").Sum(item => item.VatAmount), 2),
                TotalVatLineCalculated = Math.Round(invoices.SelectMany(item => item.Items).Sum(item => item.LineVatAmount), 2),
                VatReconciliationDifference = Math.Round(
                    invoices.Sum(item => item.VatAmount) - invoices.SelectMany(item => item.Items).Sum(item => item.LineVatAmount),
                    2),
                VatMismatchInvoices = invoices.Count(item =>
                    item.Items.Any() &&
                    Math.Round(item.Items.Sum(row => row.LineVatAmount), 2) != Math.Round(item.VatAmount, 2)),
                MonthlySalesTotal = Math.Round(invoices.Where(item => item.InvoiceType == "Sale").Sum(item => item.GrandTotal), 2),
                MonthlyPurchaseTotal = Math.Round(invoices.Where(item => item.InvoiceType == "Purchase").Sum(item => item.GrandTotal), 2),
                MonthlyTaxPayable = Math.Round(invoices.Where(item => item.InvoiceType == "Sale").Sum(item => item.VatAmount) - invoices.Where(item => item.InvoiceType == "Purchase").Sum(item => item.VatAmount), 2),
                OverdueInvoices = invoices.Count(item => !string.IsNullOrWhiteSpace(item.DeadlineDateShamsi) && item.WorkflowStatus != WorkflowStatus.Approved && item.WorkflowStatus != WorkflowStatus.Archived),
                VatRows = invoices
                    .OrderByDescending(item => item.DateShamsi)
                    .ThenByDescending(item => item.CreatedAt)
                    .Select(item => new VatInvoiceRowVM
                    {
                        Id = item.Id,
                        InvoiceNumber = item.InvoiceNumber,
                        InvoiceType = item.InvoiceType,
                        DateShamsi = item.DateShamsi,
                        PartyName = item.PartyName,
                        SubTotal = item.SubTotal,
                        VatAmount = item.VatAmount,
                        GrandTotal = item.GrandTotal
                    })
                    .ToList(),
                RecentActivities = invoices
                    .OrderByDescending(item => item.CreatedAt)
                    .Take(8)
                    .Select(item => new FinancialActivityVM
                    {
                        Title = $"{item.InvoiceNumber} - {item.PartyName}",
                        Subtitle = $"{(item.InvoiceType == "Sale" ? "فروش" : "خرید")} | {WorkflowStatus.Label(item.WorkflowStatus)} | {item.DateShamsi}",
                        Url = Url.Action(nameof(EditInvoice), new { id = item.Id }) ?? "#",
                        Badge = item.GrandTotal.ToString("N0"),
                        BadgeTone = item.InvoiceType == "Sale" ? "success" : "primary"
                    })
                    .ToList()
            };

            ViewBag.Year = targetYear;
            return View(model);
        }

        [HttpGet]
        public Task<IActionResult> VatDashboard(int? year, CancellationToken cancellationToken)
        {
            return VatDashboardInternal(year, cancellationToken);
        }

        [HttpGet]
        public async Task<IActionResult> SeasonalTaxReport(int? year, int? quarter, CancellationToken cancellationToken)
        {
            var targetYear = year ?? GetCurrentShamsiYear();
            var targetQuarter = quarter is >= 1 and <= 4 ? quarter.Value : 1;

            var rows = await _context.Invoices
                .AsNoTracking()
                .Where(item =>
                    item.DateShamsi.StartsWith(targetYear.ToString()) &&
                    GetQuarterMonthTokens(targetQuarter).Any(token => item.DateShamsi.Contains(token)) &&
                    (item.InvoiceType == "Sale" || item.InvoiceType == "Purchase"))
                .OrderBy(item => item.DateShamsi)
                .Select(item => new SeasonalTaxReportRowVM
                {
                    PartyName = item.PartyName,
                    NationalId = item.NationalCodeOrEconomicId,
                    TransactionType = item.InvoiceType == "Sale" ? "فروش" : "خرید",
                    Amount = item.SubTotal,
                    Vat = item.VatAmount,
                    DateShamsi = item.DateShamsi
                })
                .ToListAsync(cancellationToken);

            var model = new SeasonalTaxReportVM
            {
                Year = targetYear,
                Quarter = targetQuarter,
                QuarterTitle = GetQuarterTitle(targetQuarter),
                Rows = rows,
                TotalAmount = rows.Sum(item => item.Amount),
                TotalVat = rows.Sum(item => item.Vat)
            };

            return View(model);
        }

        [HttpGet]
        public Task<IActionResult> SeasonalTax(int? year, int? quarter, CancellationToken cancellationToken)
        {
            return SeasonalTaxInternal(year, quarter, cancellationToken);
        }

        [HttpGet]
        public async Task<IActionResult> ReportingHub(int? year, CancellationToken cancellationToken)
        {
            return await Dashboard(year, cancellationToken);
        }

        [HttpGet]
        [PermissionAuthorize("Finance.View")]
        public async Task<IActionResult> LedgerOperations(CancellationToken cancellationToken)
        {
            await EnsureCurrentFiscalYearAsync(cancellationToken);
            var model = await BuildLedgerOperationsModelAsync(cancellationToken);
            return View(model);
        }

        [HttpGet]
        [PermissionAuthorize("Finance.View")]
        public async Task<IActionResult> GetTrialBalance(
            Guid? fiscalPeriodId,
            bool includeMoatagh = false,
            bool groupByFloatingDetail = false,
            bool sixColumn = true,
            string? format = null,
            CancellationToken cancellationToken = default)
        {
            await EnsureCurrentFiscalYearAsync(cancellationToken);
            var model = await BuildTrialBalancePageModelAsync(
                fiscalPeriodId,
                includeMoatagh,
                groupByFloatingDetail,
                sixColumn,
                cancellationToken);

            if (string.Equals(format, "json", StringComparison.OrdinalIgnoreCase))
            {
                return Json(model.Report);
            }

            return View("TrialBalance", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Finance.Edit")]
        public async Task<IActionResult> UpdateFiscalPeriodStatus(Guid id, string status, CancellationToken cancellationToken)
        {
            if (status is not (FiscalPeriodStatus.Open or FiscalPeriodStatus.SoftLocked or FiscalPeriodStatus.HardLocked))
            {
                TempData["ErrorMessage"] = "وضعیت دوره مالی معتبر نیست.";
                return RedirectToAction(nameof(LedgerOperations));
            }

            var period = await _context.FiscalPeriods.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (period == null)
            {
                return NotFound();
            }

            period.Status = status;
            await _context.SaveChangesAsync(cancellationToken);
            TempData["SuccessMessage"] = "وضعیت دوره مالی به‌روزرسانی شد.";
            return RedirectToAction(nameof(LedgerOperations));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Finance.Edit")]
        public async Task<IActionResult> CreateExchangeRate(CurrencyExchangeRateCreateVM model, CancellationToken cancellationToken)
        {
            if (model.CurrencyId == Guid.Empty || model.BuyRate <= 0 || model.SellRate <= 0)
            {
                TempData["ErrorMessage"] = "ارز و نرخ‌های خرید/فروش باید معتبر باشند.";
                return RedirectToAction(nameof(LedgerOperations));
            }

            var rateDate = model.RateDate.Date;
            var existing = await _context.CurrencyExchangeRates
                .FirstOrDefaultAsync(item => item.CurrencyId == model.CurrencyId && item.RateDate == rateDate, cancellationToken);

            if (existing == null)
            {
                _context.CurrencyExchangeRates.Add(new CurrencyExchangeRate
                {
                    CurrencyId = model.CurrencyId,
                    RateDate = rateDate,
                    BuyRate = model.BuyRate,
                    SellRate = model.SellRate
                });
            }
            else
            {
                existing.BuyRate = model.BuyRate;
                existing.SellRate = model.SellRate;
            }

            await _context.SaveChangesAsync(cancellationToken);
            TempData["SuccessMessage"] = "نرخ ارز ذخیره شد.";
            return RedirectToAction(nameof(LedgerOperations));
        }

        [HttpGet]
        public async Task<IActionResult> GetLatestCurrencyRate(Guid currencyId, DateTime? rateDate, CancellationToken cancellationToken)
        {
            var date = (rateDate ?? DateTime.Today).Date;
            var rate = await _context.CurrencyExchangeRates
                .AsNoTracking()
                .Where(item => item.CurrencyId == currencyId && item.RateDate <= date)
                .OrderByDescending(item => item.RateDate)
                .Select(item => new { item.SellRate, item.BuyRate, item.RateDate })
                .FirstOrDefaultAsync(cancellationToken);

            return Json(rate == null
                ? new { found = false, exchangeRate = 1m }
                : new { found = true, exchangeRate = rate.SellRate, rateDate = rate.RateDate.ToString("yyyy-MM-dd") });
        }

        [HttpGet]
        [PermissionAuthorize("Finance.View")]
        public async Task<IActionResult> GetFloatingDetailsForSubsidiaryAccount(int subsidiaryAccountId, CancellationToken cancellationToken)
        {
            if (subsidiaryAccountId <= 0)
            {
                return Json(Array.Empty<object>());
            }

            var allowsFloatingDetail = await _context.SubsidiaryAccounts
                .AsNoTracking()
                .Where(item => item.Id == subsidiaryAccountId && item.IsActive)
                .Select(item => item.AllowsFloatingDetail)
                .FirstOrDefaultAsync(cancellationToken);

            if (!allowsFloatingDetail)
            {
                return Json(Array.Empty<object>());
            }

            var items = await _context.SubsidiaryAccountFloatingDetails
                .AsNoTracking()
                .Where(item => item.SubsidiaryAccountId == subsidiaryAccountId && item.FloatingDetailAccount.IsActive)
                .OrderBy(item => item.FloatingDetailAccount.Code)
                .Select(item => new
                {
                    id = item.FloatingDetailAccountId.ToString(),
                    text = item.FloatingDetailAccount.Code + " - " + item.FloatingDetailAccount.Name,
                    code = item.FloatingDetailAccount.Code,
                    name = item.FloatingDetailAccount.Name
                })
                .ToListAsync(cancellationToken);

            return Json(items);
        }

        [HttpGet]
        [PermissionAuthorize("Finance.View")]
        public async Task<IActionResult> GetSubsidiaryAccounts(string? term, CancellationToken cancellationToken)
        {
            var normalizedTerm = (term ?? string.Empty).Trim();

            var query = _context.SubsidiaryAccounts
                .AsNoTracking()
                .Where(item => item.IsActive);

            if (!string.IsNullOrWhiteSpace(normalizedTerm))
            {
                query = query.Where(item =>
                    item.Code.Contains(normalizedTerm) ||
                    item.Name.Contains(normalizedTerm));
            }

            var items = await query
                .OrderBy(item => item.Code)
                .Take(40)
                .Select(item => new
                {
                    id = item.Id,
                    text = item.Code + " - " + item.Name,
                    code = item.Code,
                    name = item.Name
                })
                .ToListAsync(cancellationToken);

            return Json(items);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Finance.Create")]
        public async Task<IActionResult> CreateVoucher(SimpleVoucherCreateVM model, CancellationToken cancellationToken)
        {
            await EnsureCurrentFiscalYearAsync(cancellationToken);
            try
            {
                await _ledgerService.CreateManualVoucherAsync(model, cancellationToken);
                TempData["SuccessMessage"] = "سند حسابداری ذخیره شد.";
                return RedirectToAction(nameof(LedgerOperations));
            }
            catch (InvalidOperationException ex)
            {
                TempData["ErrorMessage"] = ex.Message;
                return RedirectToAction(nameof(LedgerOperations));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Finance.Edit")]
        public async Task<IActionResult> RenumberVouchers(Guid fiscalPeriodId, CancellationToken cancellationToken)
        {
            if (fiscalPeriodId == Guid.Empty)
            {
                TempData["ErrorMessage"] = "دوره مالی برای بازچینی اسناد مشخص نشده است.";
                return RedirectToAction(nameof(LedgerOperations));
            }

            var affectedCount = await _voucherRenumberingService.RenumberAsync(fiscalPeriodId, cancellationToken);
            TempData["SuccessMessage"] = $"{affectedCount} سند غیرقطعی بازشماره‌گذاری شد.";
            return RedirectToAction(nameof(LedgerOperations));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Finance.Edit")]
        public async Task<IActionResult> CloseTemporaryAccounts(PeriodClosingRequestVM model, CancellationToken cancellationToken)
        {
            try
            {
                var voucher = await _periodClosingService.CloseTemporaryAccountsAsync(
                    model.FiscalPeriodId,
                    model.DestinationAccountId,
                    cancellationToken);

                TempData["SuccessMessage"] = $"سند بستن حساب‌های موقت صادر شد. شماره سند: {voucher.DocumentNumber}";
            }
            catch (InvalidOperationException ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }

            return RedirectToAction(nameof(LedgerOperations));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Finance.Edit")]
        public async Task<IActionResult> ChangeVoucherStatus([FromBody] ChangeVoucherStatusRequest request, CancellationToken cancellationToken)
        {
            try
            {
                if (!Enum.TryParse<VoucherStatus>(request.TargetStatus, true, out var targetStatus))
                {
                    Response.StatusCode = StatusCodes.Status400BadRequest;
                    return Json(new { success = false, message = "وضعیت هدف نامعتبر است." });
                }

                var voucher = await _ledgerService.ChangeVoucherStatusAsync(request.VoucherId, targetStatus, cancellationToken);
                return Json(new
                {
                    success = true,
                    voucherId = voucher.Id,
                    status = voucher.Status.ToString(),
                    message = "وضعیت سند به‌روزرسانی شد."
                });
            }
            catch (InvalidOperationException ex)
            {
                Response.StatusCode = StatusCodes.Status400BadRequest;
                return Json(new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }

        private async Task<IActionResult> VatDashboardInternal(int? year, CancellationToken cancellationToken)
        {
            var targetYear = year ?? GetCurrentShamsiYear();
            var dashboardResult = await Dashboard(targetYear, cancellationToken);
            if (dashboardResult is not ViewResult viewResult || viewResult.Model is not FinancialDashboardVM model)
            {
                return dashboardResult;
            }

            ViewBag.Year = targetYear;
            return View("VatDashboard", model);
        }

        private async Task<IActionResult> SeasonalTaxInternal(int? year, int? quarter, CancellationToken cancellationToken)
        {
            var targetYear = year ?? GetCurrentShamsiYear();
            var targetQuarter = quarter is >= 1 and <= 4 ? quarter.Value : 1;
            var reportResult = await SeasonalTaxReport(targetYear, targetQuarter, cancellationToken);
            if (reportResult is not ViewResult viewResult || viewResult.Model is not SeasonalTaxReportVM model)
            {
                return reportResult;
            }

            return View("SeasonalTax", model);
        }

        [HttpGet]
        [PermissionAuthorize("Finance.Export")]
        public async Task<IActionResult> ExportInvoicesExcel(string? invoiceType, int? year, int? quarter, CancellationToken cancellationToken)
        {
            var query = _context.Invoices.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(invoiceType))
            {
                query = query.Where(item => item.InvoiceType == invoiceType);
            }

            if (year.HasValue)
            {
                query = query.Where(item => item.DateShamsi.StartsWith(year.Value.ToString()));
            }

            if (quarter is >= 1 and <= 4)
            {
                var quarterTokens = GetQuarterMonthTokens(quarter.Value);
                query = query.Where(item => quarterTokens.Any(token => item.DateShamsi.Contains(token)));
            }

            var items = await query.OrderByDescending(item => item.DateShamsi).ToListAsync(cancellationToken);

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Invoices");

            worksheet.RightToLeft = true;
            worksheet.Cell(1, 1).Value = "شماره";
            worksheet.Cell(1, 2).Value = "نوع";
            worksheet.Cell(1, 3).Value = "تاریخ";
            worksheet.Cell(1, 4).Value = "طرف معامله";
            worksheet.Cell(1, 5).Value = "کد ملی/اقتصادی";
            worksheet.Cell(1, 6).Value = "جمع جزء";
            worksheet.Cell(1, 7).Value = "مالیات 10%";
            worksheet.Cell(1, 8).Value = "جمع کل";

            for (var index = 0; index < items.Count; index++)
            {
                var row = index + 2;
                var item = items[index];
                worksheet.Cell(row, 1).Value = item.InvoiceNumber;
                worksheet.Cell(row, 2).Value = item.InvoiceType == "Sale" ? "فروش" : "خرید";
                worksheet.Cell(row, 3).Value = item.DateShamsi;
                worksheet.Cell(row, 4).Value = item.PartyName;
                worksheet.Cell(row, 5).Value = item.NationalCodeOrEconomicId;
                worksheet.Cell(row, 6).Value = item.SubTotal;
                worksheet.Cell(row, 7).Value = item.VatAmount;
                worksheet.Cell(row, 8).Value = item.GrandTotal;
            }

            worksheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "financial-invoices.xlsx");
        }

        [HttpGet]
        [PermissionAuthorize("Finance.Export")]
        public async Task<IActionResult> ExportStockExcel(int? warehouseId, CancellationToken cancellationToken)
        {
            var query = _inventoryContext.InventoryStocks
                .AsNoTracking()
                .Include(item => item.Product)
                .Include(item => item.Warehouse)
                .AsQueryable();

            if (warehouseId.HasValue)
            {
                query = query.Where(item => item.WarehouseId == warehouseId.Value);
            }

            var items = await query.OrderBy(item => item.Product.Name).ToListAsync(cancellationToken);

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Stock");
            worksheet.RightToLeft = true;

            worksheet.Cell(1, 1).Value = "کد کالا";
            worksheet.Cell(1, 2).Value = "نام کالا";
            worksheet.Cell(1, 3).Value = "انبار";
            worksheet.Cell(1, 4).Value = "واحد";
            worksheet.Cell(1, 5).Value = "موجودی";

            for (var index = 0; index < items.Count; index++)
            {
                var row = index + 2;
                worksheet.Cell(row, 1).Value = items[index].Product.Code;
                worksheet.Cell(row, 2).Value = items[index].Product.Name;
                worksheet.Cell(row, 3).Value = items[index].Warehouse.Name;
                worksheet.Cell(row, 4).Value = items[index].Product.Unit;
                worksheet.Cell(row, 5).Value = items[index].CurrentQuantity;
            }

            worksheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "inventory-stock.xlsx");
        }

        [HttpGet]
        [PermissionAuthorize("Finance.Export")]
        public async Task<IActionResult> ExportPayrollExcel(int? year, int? month, CancellationToken cancellationToken)
        {
            var query = _context.PayrollLists
                .AsNoTracking()
                .Include(item => item.Items)
                .AsQueryable();

            if (year.HasValue)
            {
                query = query.Where(item => item.Year == year.Value);
            }

            if (month.HasValue)
            {
                query = query.Where(item => item.Month == month.Value);
            }

            var payrolls = await query.OrderByDescending(item => item.Year).ThenByDescending(item => item.Month).ToListAsync(cancellationToken);

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Payroll");
            worksheet.RightToLeft = true;

            worksheet.Cell(1, 1).Value = "سال";
            worksheet.Cell(1, 2).Value = "ماه";
            worksheet.Cell(1, 3).Value = "کارمند";
            worksheet.Cell(1, 4).Value = "حقوق پایه";
            worksheet.Cell(1, 5).Value = "مزایا";
            worksheet.Cell(1, 6).Value = "اضافه‌کار";
            worksheet.Cell(1, 7).Value = "بیمه";
            worksheet.Cell(1, 8).Value = "مالیات";
            worksheet.Cell(1, 9).Value = "خالص پرداختی";

            var rowIndex = 2;
            foreach (var payroll in payrolls)
            {
                foreach (var item in payroll.Items.OrderBy(row => row.EmployeeName))
                {
                    worksheet.Cell(rowIndex, 1).Value = payroll.Year;
                    worksheet.Cell(rowIndex, 2).Value = payroll.Month;
                    worksheet.Cell(rowIndex, 3).Value = item.EmployeeName;
                    worksheet.Cell(rowIndex, 4).Value = item.BaseSalary;
                    worksheet.Cell(rowIndex, 5).Value = item.Allowance;
                    worksheet.Cell(rowIndex, 6).Value = item.Overtime;
                    worksheet.Cell(rowIndex, 7).Value = item.InsuranceDeduction;
                    worksheet.Cell(rowIndex, 8).Value = item.Tax;
                    worksheet.Cell(rowIndex, 9).Value = item.NetPayable;
                    rowIndex++;
                }
            }

            worksheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "payroll.xlsx");
        }

        [HttpGet]
        public async Task<IActionResult> PrintInvoicePdf(int id, CancellationToken cancellationToken)
        {
            var invoice = await _context.Invoices
                .AsNoTracking()
                .Include(item => item.Items)
                .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

            if (invoice == null)
            {
                return NotFound();
            }

            return View("PrintInvoice", invoice);
        }

        [HttpGet]
        public async Task<IActionResult> PrintWarehouseReceiptPdf(int id, CancellationToken cancellationToken)
        {
            var receipt = await _inventoryContext.WarehouseReceipts
                .AsNoTracking()
                .Include(item => item.Warehouse)
                .Include(item => item.Items)
                .ThenInclude(item => item.Product)
                .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

            if (receipt == null)
            {
                return NotFound();
            }

            return View("PrintWarehouseReceipt", receipt);
        }

        [HttpGet]
        public async Task<IActionResult> PrintPayslipPdf(int payrollListId, int payrollItemId, CancellationToken cancellationToken)
        {
            var payroll = await _context.PayrollLists
                .AsNoTracking()
                .Include(item => item.Items)
                .FirstOrDefaultAsync(item => item.Id == payrollListId, cancellationToken);

            if (payroll == null)
            {
                return NotFound();
            }

            var payrollItem = payroll.Items.FirstOrDefault(item => item.Id == payrollItemId);
            if (payrollItem == null)
            {
                return NotFound();
            }

            ViewBag.PayrollYear = payroll.Year;
            ViewBag.PayrollMonth = payroll.Month;
            return View("PrintPayslip", payrollItem);
        }

        private async Task<FinanceLedgerOperationsVM> BuildLedgerOperationsModelAsync(CancellationToken cancellationToken)
        {
            var accessTokens = await GetCurrentAccessTokensAsync(cancellationToken);
            var visibleColumns = _tableSchemaRegistry
                .GetAllowedColumns("Finance_Vouchers", accessTokens)
                .Select(item => item.ColumnId)
                .ToList();

            var currencies = await _context.Currencies
                .AsNoTracking()
                .Where(item => item.IsActive)
                .OrderBy(item => item.Code)
                .ToListAsync(cancellationToken);

            var accounts = await _context.SubsidiaryAccounts
                .AsNoTracking()
                .Where(item => item.IsActive)
                .OrderBy(item => item.Code)
                .Select(item => new SelectListItem { Value = item.Id.ToString(), Text = item.Code + " - " + item.Name })
                .ToListAsync(cancellationToken);

            var journals = await _context.JournalTypes
                .AsNoTracking()
                .Where(item => item.IsActive)
                .OrderBy(item => item.Code)
                .Select(item => new SelectListItem { Value = item.Id.ToString(), Text = item.Code + " - " + item.Name })
                .ToListAsync(cancellationToken);

            if (journals.Count == 0)
            {
                var journal = await EnsureDefaultJournalTypeAsync(cancellationToken);
                journals.Add(new SelectListItem { Value = journal.Id.ToString(), Text = journal.Code + " - " + journal.Name });
            }

            var defaultClosingDestinationAccountId = await _context.SubsidiaryAccounts
                .AsNoTracking()
                .Where(item => item.IsActive && item.SystemKey == FinanceAccountKeys.RetainedEarnings)
                .Select(item => item.Id)
                .FirstOrDefaultAsync(cancellationToken);

            var currentFiscalYear = await _context.FiscalYears
                .AsNoTracking()
                .Where(item => item.StartDate <= DateTime.Today && item.EndDate >= DateTime.Today)
                .OrderByDescending(item => item.StartDate)
                .FirstOrDefaultAsync(cancellationToken);

            var periodQuery = _context.FiscalPeriods.AsNoTracking().AsQueryable();
            if (currentFiscalYear != null)
            {
                periodQuery = periodQuery.Where(item => item.FiscalYearId == currentFiscalYear.Id);
            }

            var currentPeriodId = currentFiscalYear == null
                ? null
                : await _context.FiscalPeriods
                    .AsNoTracking()
                    .Where(item =>
                        item.FiscalYearId == currentFiscalYear.Id &&
                        item.StartDate <= DateTime.Today &&
                        item.EndDate >= DateTime.Today)
                    .Select(item => (Guid?)item.Id)
                    .FirstOrDefaultAsync(cancellationToken);

            return new FinanceLedgerOperationsVM
            {
                FiscalPeriods = await periodQuery
                    .OrderBy(item => item.PeriodNumber)
                    .Select(item => new FiscalPeriodRowVM
                    {
                        Id = item.Id,
                        Name = item.Name,
                        PeriodNumber = item.PeriodNumber,
                        StartDate = item.StartDate,
                        EndDate = item.EndDate,
                        Status = item.Status
                    })
                    .ToListAsync(cancellationToken),
                ExchangeRates = await _context.CurrencyExchangeRates
                    .AsNoTracking()
                    .Include(item => item.Currency)
                    .OrderByDescending(item => item.RateDate)
                    .ThenBy(item => item.Currency.Code)
                    .Take(20)
                    .Select(item => new CurrencyRateRowVM
                    {
                        CurrencyCode = item.Currency.Code,
                        RateDate = item.RateDate,
                        BuyRate = item.BuyRate,
                        SellRate = item.SellRate
                    })
                    .ToListAsync(cancellationToken),
                Vouchers = await _context.VoucherHeaders
                    .AsNoTracking()
                    .Include(item => item.JournalType)
                    .Include(item => item.Lines)
                    .ThenInclude(item => item.FloatingDetailAccount)
                    .OrderByDescending(item => item.VoucherDate)
                    .ThenByDescending(item => item.Id)
                    .Take(50)
                    .Select(item => new VoucherGridRowVM
                    {
                        Id = item.Id,
                        SequenceNumber = item.SequenceNumber,
                        VoucherNumber = item.VoucherNumber,
                        VoucherDate = item.VoucherDate,
                        Description = item.Description ?? string.Empty,
                        JournalType = item.JournalType.Code,
                        TotalAmount = item.TotalDebits,
                        Status = item.Status,
                        PostingStatus = item.PostingStatus
                    })
                    .ToListAsync(cancellationToken),
                VisibleVoucherColumns = visibleColumns,
                CurrentFiscalPeriodId = currentPeriodId,
                CurrencyOptions = currencies
                    .Where(item => !item.IsBaseCurrency)
                    .Select(item => new SelectListItem { Value = item.Id.ToString(), Text = item.Code + " - " + item.Name })
                    .ToList(),
                CurrencyLookupOptions = currencies
                    .Where(item => !item.IsBaseCurrency)
                    .Select(item => new SelectListItem { Value = item.Id.ToString(), Text = item.Code + " - " + item.Name })
                    .ToList(),
                AccountOptions = accounts,
                JournalTypeOptions = journals,
                ExchangeRate = new CurrencyExchangeRateCreateVM
                {
                    RateDate = DateTime.Today,
                    CurrencyId = currencies.FirstOrDefault(item => !item.IsBaseCurrency)?.Id ?? Guid.Empty
                },
                Voucher = new SimpleVoucherCreateVM
                {
                    VoucherDate = DateTime.Today,
                    JournalTypeId = int.TryParse(journals.FirstOrDefault()?.Value, out var journalId) ? journalId : 0
                },
                PeriodClosing = new PeriodClosingRequestVM
                {
                    FiscalPeriodId = currentPeriodId ?? Guid.Empty,
                    DestinationAccountId = defaultClosingDestinationAccountId
                }
            };
        }

        private async Task<TrialBalancePageVM> BuildTrialBalancePageModelAsync(
            Guid? fiscalPeriodId,
            bool includeMoatagh,
            bool groupByFloatingDetail,
            bool sixColumn,
            CancellationToken cancellationToken)
        {
            var periods = await _context.FiscalPeriods
                .AsNoTracking()
                .Include(item => item.FiscalYear)
                .OrderByDescending(item => item.StartDate)
                .ThenByDescending(item => item.PeriodNumber)
                .Select(item => new
                {
                    item.Id,
                    item.Name,
                    item.StartDate,
                    item.EndDate,
                    FiscalYearName = item.FiscalYear.YearName
                })
                .ToListAsync(cancellationToken);

            var selectedPeriodId = fiscalPeriodId.GetValueOrDefault();
            if (selectedPeriodId == Guid.Empty)
            {
                selectedPeriodId = periods
                    .Where(item => item.StartDate.Date <= DateTime.Today && item.EndDate.Date >= DateTime.Today)
                    .Select(item => item.Id)
                    .FirstOrDefault();
            }

            if (selectedPeriodId == Guid.Empty)
            {
                selectedPeriodId = periods.Select(item => item.Id).FirstOrDefault();
            }

            var report = selectedPeriodId == Guid.Empty
                ? new TrialBalanceDto()
                : await _trialBalanceService.GetTrialBalanceAsync(
                    selectedPeriodId,
                    includeMoatagh,
                    groupByFloatingDetail,
                    cancellationToken);

            return new TrialBalancePageVM
            {
                FiscalPeriodId = selectedPeriodId,
                IncludeMoatagh = includeMoatagh,
                GroupByFloatingDetail = groupByFloatingDetail,
                SixColumn = sixColumn,
                FiscalPeriodOptions = periods
                    .OrderBy(item => item.StartDate)
                    .ThenBy(item => item.Name)
                    .Select(item => new SelectListItem
                    {
                        Value = item.Id.ToString(),
                        Text = item.FiscalYearName + " - " + item.Name,
                        Selected = item.Id == selectedPeriodId
                    })
                    .ToList(),
                Report = report
            };
        }

        private async Task<IReadOnlyCollection<string>> GetCurrentAccessTokensAsync(CancellationToken cancellationToken)
        {
            var profile = await _currentUserContextAccessor.GetAccessProfileAsync(cancellationToken);
            var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (profile != null)
            {
                foreach (var role in profile.Roles)
                {
                    tokens.Add(role);
                }

                foreach (var permission in profile.Permissions)
                {
                    tokens.Add(permission);
                }
            }

            foreach (var role in User.FindAll(System.Security.Claims.ClaimTypes.Role).Select(item => item.Value))
            {
                tokens.Add(role);
            }

            return tokens;
        }

        private async Task EnsureCurrentFiscalYearAsync(CancellationToken cancellationToken)
        {
            var year = GetCurrentShamsiYear();
            var yearName = year.ToString(CultureInfo.InvariantCulture);
            if (await _context.FiscalYears.AnyAsync(item => item.YearName == yearName, cancellationToken))
            {
                return;
            }

            var persianCalendar = new PersianCalendar();
            var fiscalYear = new FiscalYear
            {
                YearName = yearName,
                StartDate = persianCalendar.ToDateTime(year, 1, 1, 0, 0, 0, 0),
                EndDate = persianCalendar.ToDateTime(year, 12, persianCalendar.GetDaysInMonth(year, 12), 23, 59, 59, 999),
                FiscalPeriods = Enumerable.Range(1, 12)
                    .Select(month =>
                    {
                        var days = persianCalendar.GetDaysInMonth(year, month);
                        return new FiscalPeriod
                        {
                            Name = $"{yearName}/{month:00}",
                            PeriodNumber = month,
                            StartDate = persianCalendar.ToDateTime(year, month, 1, 0, 0, 0, 0),
                            EndDate = persianCalendar.ToDateTime(year, month, days, 23, 59, 59, 999),
                            Status = FiscalPeriodStatus.Open
                        };
                    })
                    .ToList()
            };

            _context.FiscalYears.Add(fiscalYear);
            await _context.SaveChangesAsync(cancellationToken);
        }

        private async Task<JournalType> EnsureDefaultJournalTypeAsync(CancellationToken cancellationToken)
        {
            var journal = await _context.JournalTypes.FirstOrDefaultAsync(item => item.Code == JournalTypeCodes.General, cancellationToken);
            if (journal != null)
            {
                return journal;
            }

            journal = new JournalType { Code = JournalTypeCodes.General, Name = "General Journal", IsActive = true };
            _context.JournalTypes.Add(journal);
            await _context.SaveChangesAsync(cancellationToken);
            return journal;
        }

        private async Task ValidateInvoiceAsync(FinancialInvoiceUpsertVM model, CancellationToken cancellationToken, int? currentInvoiceId = null)
        {
            if (model.InvoiceType != "Sale" && model.InvoiceType != "Purchase")
            {
                ModelState.AddModelError(nameof(model.InvoiceType), "نوع فاکتور باید Sale یا Purchase باشد.");
            }

            if (!TryParseShamsiDate(model.DateShamsi, out _))
            {
                ModelState.AddModelError(nameof(model.DateShamsi), "فرمت تاریخ شمسی نامعتبر است.");
            }

            var normalizedNumber = model.InvoiceNumber?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalizedNumber))
            {
                return;
            }

            if (model.InvoiceType == "Sale" && !model.EmployerId.HasValue)
            {
                ModelState.AddModelError(nameof(model.EmployerId), "انتخاب کارفرما برای فاکتور فروش الزامی است.");
            }

            var duplicate = await _context.Invoices
                .AsNoTracking()
                .AnyAsync(item =>
                    item.InvoiceNumber == normalizedNumber &&
                    item.InvoiceType == model.InvoiceType &&
                    (!currentInvoiceId.HasValue || item.Id != currentInvoiceId.Value),
                    cancellationToken);

            if (duplicate)
            {
                ModelState.AddModelError(nameof(model.InvoiceNumber), "شماره فاکتور برای این نوع، تکراری است.");
            }

            var validItems = model.Items.Where(item => !string.IsNullOrWhiteSpace(item.ItemName) && item.Quantity > 0).ToList();
            if (validItems.Count == 0)
            {
                ModelState.AddModelError(nameof(model.Items), "حداقل یک ردیف معتبر برای فاکتور وارد کنید.");
            }

            var productIds = validItems.Where(item => item.ProductId.HasValue).Select(item => item.ProductId!.Value).Distinct().ToList();
            if (productIds.Count > 0)
            {
                var validProductIds = await _inventoryContext.Products
                    .AsNoTracking()
                    .Where(item => item.IsActive && !item.IsDeleted && productIds.Contains(item.Id))
                    .Select(item => item.Id)
                    .ToListAsync(cancellationToken);

                if (validProductIds.Count != productIds.Count)
                {
                    ModelState.AddModelError(nameof(model.Items), "برخی کالاهای انتخاب‌شده نامعتبر یا غیرفعال هستند.");
                }
            }

            if (model.InvoiceType == "Purchase")
            {
                model.EmployerId = null;
                if (!string.IsNullOrWhiteSpace(model.DeadlineDateShamsi) && !TryParseShamsiDate(model.DeadlineDateShamsi, out _))
                {
                    ModelState.AddModelError(nameof(model.DeadlineDateShamsi), "فرمت مهلت پیگیری نامعتبر است.");
                }

                if (model.WarehouseReceiptId.HasValue)
                {
                    var warehouseReceiptId = model.WarehouseReceiptId.Value;
                    var receiptExists = await _inventoryContext.WarehouseReceipts
                        .AsNoTracking()
                        .AnyAsync(item => item.Id == warehouseReceiptId, cancellationToken);

                    if (!receiptExists)
                    {
                        ModelState.AddModelError(nameof(model.WarehouseReceiptId), "رسید انبار انتخاب‌شده معتبر نیست.");
                    }
                    else
                    {
                        var receiptMappedToOtherInvoice = await _context.Invoices
                            .AsNoTracking()
                            .AnyAsync(item =>
                                item.WarehouseReceiptId == warehouseReceiptId &&
                                item.InvoiceType == "Purchase" &&
                                (!currentInvoiceId.HasValue || item.Id != currentInvoiceId.Value),
                                cancellationToken);

                        if (receiptMappedToOtherInvoice)
                        {
                            ModelState.AddModelError(nameof(model.WarehouseReceiptId), "این رسید انبار قبلاً به فاکتور خرید دیگری متصل شده است.");
                        }
                    }
                }

                if (model.FollowUpEmployeeId.HasValue)
                {
                    var employeeExists = await _officeContext.HumanCapitalEmployees
                        .AsNoTracking()
                        .AnyAsync(item => item.Id == model.FollowUpEmployeeId.Value && item.CurrentStatus == "فعال", cancellationToken);

                    if (!employeeExists)
                    {
                        ModelState.AddModelError(nameof(model.FollowUpEmployeeId), "مسئول پیگیری انتخاب‌شده معتبر یا فعال نیست.");
                    }
                }
            }
            else if (model.InvoiceType == "Sale" && model.EmployerId.HasValue)
            {
                var employerExists = await _context.Employers
                    .AsNoTracking()
                    .AnyAsync(item => item.Id == model.EmployerId.Value && item.IsActive, cancellationToken);
                if (!employerExists)
                {
                    ModelState.AddModelError(nameof(model.EmployerId), "کارفرمای انتخابی معتبر یا فعال نیست.");
                }
            }
        }

        private async Task<FinancialInvoiceIndexVM> BuildInvoiceIndexAsync(FinancialInvoiceIndexVM filter, string invoiceType, CancellationToken cancellationToken)
        {
            return await _invoiceService.BuildInvoiceIndexAsync(filter, invoiceType, cancellationToken);
        }

        private async Task PopulateProductOptionsAsync(List<SelectListItem> options, CancellationToken cancellationToken)
        {
            options.Clear();
            var items = await _inventoryContext.Products
                .AsNoTracking()
                .Where(item => item.IsActive && !item.IsDeleted)
                .OrderBy(item => item.Name)
                .Select(item => new SelectListItem
                {
                    Value = item.Id.ToString(),
                    Text = $"{item.Name} ({item.Code})"
                })
                .ToListAsync(cancellationToken);

            options.AddRange(items);
        }

        private async Task PopulatePurchaseOptionsAsync(FinancialInvoiceUpsertVM model, CancellationToken cancellationToken)
        {
            model.WarehouseReceiptOptions.Clear();
            model.FollowUpEmployeeOptions.Clear();

            var mappedReceiptIds = await _context.Invoices
                .AsNoTracking()
                .Where(item =>
                    item.InvoiceType == "Purchase" &&
                    item.WarehouseReceiptId.HasValue &&
                    (!model.Id.HasValue || item.Id != model.Id.Value))
                .Select(item => item.WarehouseReceiptId!.Value)
                .ToListAsync(cancellationToken);

            var receiptItems = await _inventoryContext.WarehouseReceipts
                .AsNoTracking()
                .Where(item =>
                    !mappedReceiptIds.Contains(item.Id) ||
                    (model.WarehouseReceiptId.HasValue && item.Id == model.WarehouseReceiptId.Value))
                .OrderByDescending(item => item.CreatedAt)
                .Select(item => new
                {
                    item.Id,
                    item.ReceiptNumber,
                    item.SupplierOrSource,
                    item.DateShamsi
                })
                .Take(300)
                .ToListAsync(cancellationToken);

            model.WarehouseReceiptOptions.AddRange(receiptItems.Select(item => new SelectListItem
            {
                Value = item.Id.ToString(),
                Text = $"{item.ReceiptNumber} - {item.SupplierOrSource} ({item.DateShamsi})"
            }));

            var employeeItems = await _officeContext.HumanCapitalEmployees
                .AsNoTracking()
                .Where(item => item.CurrentStatus == "فعال")
                .OrderBy(item => item.FullName)
                .Select(item => new SelectListItem
                {
                    Value = item.Id.ToString(),
                    Text = $"{item.FullName} ({item.PersonnelCode})"
                })
                .ToListAsync(cancellationToken);

            model.FollowUpEmployeeOptions.AddRange(employeeItems);
        }

        private async Task PopulateEmployerOptionsAsync(List<SelectListItem> options, CancellationToken cancellationToken)
        {
            options.Clear();
            var employers = await _context.Employers
                .AsNoTracking()
                .Where(item => item.IsActive)
                .OrderBy(item => item.Name)
                .Select(item => new SelectListItem
                {
                    Value = item.Id.ToString(),
                    Text = string.IsNullOrWhiteSpace(item.ContractNumber)
                        ? item.Name
                        : $"{item.Name} ({item.ContractNumber})"
                })
                .ToListAsync(cancellationToken);

            options.AddRange(employers);
        }

        private async Task<string> BuildNextInvoiceNumberAsync(CancellationToken cancellationToken)
        {
            var count = await _context.Invoices.CountAsync(cancellationToken);
            return $"FIN-{count + 1:00000}";
        }

        private static string GetTodayShamsi()
        {
            var persianCalendar = new PersianCalendar();
            var now = DateTime.Now;
            return $"{persianCalendar.GetYear(now):0000}/{persianCalendar.GetMonth(now):00}/{persianCalendar.GetDayOfMonth(now):00}";
        }

        private static int GetCurrentShamsiYear()
        {
            var persianCalendar = new PersianCalendar();
            return persianCalendar.GetYear(DateTime.Now);
        }

        private static List<string> GetQuarterMonthTokens(int quarter)
        {
            var months = quarter switch
            {
                1 => new[] { 1, 2, 3 },
                2 => new[] { 4, 5, 6 },
                3 => new[] { 7, 8, 9 },
                _ => new[] { 10, 11, 12 }
            };

            return months.Select(item => $"/{item:00}/").ToList();
        }

        private static string GetQuarterTitle(int quarter)
        {
            return quarter switch
            {
                1 => "بهار",
                2 => "تابستان",
                3 => "پاییز",
                _ => "زمستان"
            };
        }

        private static string? NormalizeOptionalShamsi(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return null;
            }

            return PersianTextNormalizer.Normalize(input);
        }

        private async Task<string> ResolveEmployerNameAsync(int? employerId, string? fallbackName, CancellationToken cancellationToken)
        {
            if (employerId.HasValue)
            {
                var employerName = await _context.Employers
                    .AsNoTracking()
                    .Where(item => item.Id == employerId.Value)
                    .Select(item => item.Name)
                    .FirstOrDefaultAsync(cancellationToken);
                if (!string.IsNullOrWhiteSpace(employerName))
                {
                    return employerName;
                }
            }

            return string.IsNullOrWhiteSpace(fallbackName) ? "نامشخص" : fallbackName.Trim();
        }

        private static bool TryParseShamsiDate(string? value, out DateTime result)
        {
            result = default;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var normalized = PersianTextNormalizer.Normalize(value).Replace('-', '/');
            var parts = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length != 3)
            {
                return false;
            }

            if (!int.TryParse(parts[0], out var year) ||
                !int.TryParse(parts[1], out var month) ||
                !int.TryParse(parts[2], out var day))
            {
                return false;
            }

            try
            {
                var persianCalendar = new PersianCalendar();
                result = persianCalendar.ToDateTime(year, month, day, 0, 0, 0, 0);
                return true;
            }
            catch
            {
                return false;
            }
        }

    }
}

