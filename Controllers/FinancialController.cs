using OfficeAutomation.Services.Licensing;
using System.Globalization;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Filters;
using OfficeAutomation.Models;
using OfficeAutomation.Modules.Finance.Application;
using OfficeAutomation.Modules.Finance.Domain;
using OfficeAutomation.Modules.Finance.Infrastructure.Persistence;
using OfficeAutomation.Services;
using OfficeAutomation.Services.Security;

namespace OfficeAutomation.Controllers
{
    [Authorize]
    [RequireAccessArea("Finance")]
    [RequiresModule(LicensableModules.Financial)]
public class FinancialController : Controller
    {
        private readonly FinanceDbContext _context;
        private readonly IInventoryService _inventoryService;
        private readonly FinancialInvoiceService _invoiceService;
        private readonly FinanceLedgerService _ledgerService;
        private readonly PeriodClosingService _periodClosingService;
        private readonly VoucherRenumberingService _voucherRenumberingService;
        private readonly LedgerService _ledgerReportService;
        private readonly FinancialAppService _appService;

        public FinancialController(
            FinanceDbContext context,
            IInventoryService inventoryService,
            FinancialInvoiceService invoiceService,
            FinanceLedgerService ledgerService,
            PeriodClosingService periodClosingService,
            VoucherRenumberingService voucherRenumberingService,
            LedgerService ledgerReportService,
            FinancialAppService appService)
        {
            _context = context;
            _inventoryService = inventoryService;
            _invoiceService = invoiceService;
            _ledgerService = ledgerService;
            _periodClosingService = periodClosingService;
            _voucherRenumberingService = voucherRenumberingService;
            _ledgerReportService = ledgerReportService;
            _appService = appService;
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
            var model = await _appService.BuildInvoiceIndexAsync(filter, "Sale", cancellationToken);
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
            var model = await _appService.BuildInvoiceIndexAsync(filter, "Purchase", cancellationToken);
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
            var model = await _appService.PrepareCreateInvoiceModelAsync(invoiceType, cancellationToken);
            ViewBag.BackToListAction = model.InvoiceType == "Purchase" ? nameof(Purchases) : nameof(Sales);
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> CheckInvoiceNumber(string? invoiceNumber, string? invoiceType, int? currentInvoiceId, CancellationToken cancellationToken)
        {
            var (isDuplicate, message) = await _appService.CheckInvoiceNumberAsync(invoiceNumber, invoiceType, currentInvoiceId, cancellationToken);
            return Json(new { isDuplicate, message });
        }

        [HttpGet]
        public async Task<IActionResult> GetReceiptItemsJson(int receiptId, CancellationToken cancellationToken)
        {
            var result = await _appService.GetReceiptItemsJsonAsync(receiptId, cancellationToken);
            if (result == null)
            {
                return NotFound(new { success = false, message = "رسید انبار یافت نشد." });
            }
            return Json(result);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Finance.Create")]
        public async Task<IActionResult> CreateInvoice(FinancialInvoiceUpsertVM model, CancellationToken cancellationToken)
        {
            await _appService.ValidateInvoiceAsync(model, (key, message) => ModelState.AddModelError(key, message), cancellationToken);

            if (!ModelState.IsValid)
            {
                await PopulateInvoiceDropdownsAsync(model, cancellationToken);
                ViewBag.BackToListAction = model.InvoiceType == "Purchase" ? nameof(Purchases) : nameof(Sales);
                return View(model);
            }

            var currentUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
            try
            {
                await _appService.CreateInvoiceAsync(model, currentUserId, cancellationToken);
            }
            catch (DbUpdateException)
            {
                ModelState.AddModelError(nameof(model.InvoiceNumber), "شماره فاکتور برای نوع انتخابی قبلاً ثبت شده است.");
                await PopulateInvoiceDropdownsAsync(model, cancellationToken);
                ViewBag.BackToListAction = model.InvoiceType == "Purchase" ? nameof(Purchases) : nameof(Sales);
                return View(model);
            }

            var targetAction = model.InvoiceType == "Purchase" ? nameof(Purchases) : nameof(Sales);
            return RedirectToAction(targetAction);
        }

        [HttpGet]
        [PermissionAuthorize("Finance.Edit")]
        public async Task<IActionResult> EditInvoice(int id, CancellationToken cancellationToken)
        {
            var model = await _appService.PrepareEditInvoiceModelAsync(id, cancellationToken);
            if (model == null)
            {
                return NotFound();
            }

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

            await _appService.ValidateInvoiceAsync(model, (key, message) => ModelState.AddModelError(key, message), cancellationToken, id);

            if (!ModelState.IsValid)
            {
                await PopulateInvoiceDropdownsAsync(model, cancellationToken);
                ViewBag.BackToListAction = model.InvoiceType == "Purchase" ? nameof(Purchases) : nameof(Sales);
                return View(model);
            }

            var entity = await _context.Invoices.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (entity == null)
            {
                return NotFound();
            }

            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                await _appService.UpdateInvoiceAsync(id, model, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                await transaction.RollbackAsync(cancellationToken);
                ModelState.AddModelError(nameof(model.InvoiceNumber), "شماره فاکتور برای نوع انتخابی قبلاً ثبت شده است.");
                await PopulateInvoiceDropdownsAsync(model, cancellationToken);
                ViewBag.BackToListAction = model.InvoiceType == "Purchase" ? nameof(Purchases) : nameof(Sales);
                return View(model);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                ModelState.AddModelError(string.Empty, "ویرایش فاکتور انجام نشد. لطفاً دوباره تلاش کنید.");
                await PopulateInvoiceDropdownsAsync(model, cancellationToken);
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
            var currentUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
            var (success, errorMessage) = await _appService.ApproveInvoiceAsync(id, currentUserId, cancellationToken);

            if (errorMessage == "Forbidden")
            {
                return Forbid();
            }

            if (!success)
            {
                var invoice = await _context.Invoices.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
                TempData["ErrorMessage"] = errorMessage ?? "خطا در تایید فاکتور.";
                return RedirectToInvoiceList(returnTo, invoice?.InvoiceType ?? "Sale");
            }

            var confirmedInvoice = await _context.Invoices.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
            TempData["SuccessMessage"] = "فاکتور تایید شد.";
            return RedirectToInvoiceList(returnTo, confirmedInvoice?.InvoiceType ?? "Sale");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Finance.Approve")]
        public async Task<IActionResult> RejectInvoice(int id, string? returnTo, CancellationToken cancellationToken)
        {
            var currentUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
            var (success, errorMessage) = await _appService.RejectInvoiceAsync(id, currentUserId, cancellationToken);

            if (errorMessage == "Forbidden")
            {
                return Forbid();
            }

            var invoice = await _context.Invoices.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (!success)
            {
                TempData["ErrorMessage"] = "خطا در رد فاکتور.";
                return RedirectToInvoiceList(returnTo, invoice?.InvoiceType ?? "Sale");
            }

            TempData["SuccessMessage"] = "فاکتور رد شد.";
            return RedirectToInvoiceList(returnTo, invoice?.InvoiceType ?? "Sale");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Finance.Delete")]
        public async Task<IActionResult> DeleteInvoice(int id, string? returnTo, CancellationToken cancellationToken)
        {
            var normalizedReturn = string.Equals(returnTo, "Purchase", StringComparison.OrdinalIgnoreCase)
                ? nameof(Purchases)
                : nameof(Sales);

            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var (success, invoiceType) = await _appService.DeleteInvoiceAsync(id, cancellationToken);
                if (!success)
                {
                    TempData["ErrorMessage"] = "فاکتور موردنظر یافت نشد.";
                    return RedirectToAction(normalizedReturn);
                }

                await transaction.CommitAsync(cancellationToken);
                TempData["SuccessMessage"] = "فاکتور با موفقیت حذف شد.";
                var targetRedirect = invoiceType == "Purchase" ? nameof(Purchases) : nameof(Sales);
                return RedirectToAction(targetRedirect);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                TempData["ErrorMessage"] = "حذف فاکتور انجام نشد. لطفاً دوباره تلاش کنید.";
                return RedirectToAction(normalizedReturn);
            }
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
            var targetYear = year ?? FinancialAppService.GetCurrentShamsiYear();
            var model = await _appService.BuildDashboardAsync(year, action => Url.Action(action), cancellationToken);
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
            var model = await _appService.BuildSeasonalTaxReportAsync(year, quarter, cancellationToken);
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
        public IActionResult ChartOfAccounts()
        {
            return View();
        }

        [HttpGet]
        [PermissionAuthorize("Finance.View")]
        public async Task<IActionResult> LedgerOperations(CancellationToken cancellationToken)
        {
            await _appService.EnsureCurrentFiscalYearAsync(cancellationToken);
            var model = await _appService.BuildLedgerOperationsModelAsync(cancellationToken);
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
            await _appService.EnsureCurrentFiscalYearAsync(cancellationToken);
            var model = await _appService.BuildTrialBalancePageModelAsync(fiscalPeriodId, includeMoatagh, groupByFloatingDetail, sixColumn, cancellationToken);

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
            var result = await _appService.GetLatestCurrencyRateAsync(currencyId, rateDate, cancellationToken);
            return Json(result);
        }

        [HttpGet]
        [PermissionAuthorize("Finance.View")]
        public async Task<IActionResult> GetFloatingDetailsForSubsidiaryAccount(int subsidiaryAccountId, CancellationToken cancellationToken)
        {
            var items = await _appService.GetFloatingDetailsForSubsidiaryAccountAsync(subsidiaryAccountId, cancellationToken);
            return Json(items);
        }

        [HttpGet]
        [PermissionAuthorize("Finance.View")]
        public async Task<IActionResult> GetSubsidiaryAccounts(string? term, CancellationToken cancellationToken)
        {
            var items = await _appService.GetSubsidiaryAccountsAsync(term, cancellationToken);
            return Json(items);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Finance.Create")]
        public async Task<IActionResult> CreateVoucher(SimpleVoucherCreateVM model, CancellationToken cancellationToken)
        {
            await _appService.EnsureCurrentFiscalYearAsync(cancellationToken);
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Finance.Edit")]
        public async Task<IActionResult> CopyVoucher(int id, DateTime? newDate, string? newDescription, string? newReference, bool swapDebitCredit, CancellationToken cancellationToken)
        {
            var currentUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            try
            {
                var voucher = await _ledgerService.CopyVoucherAsync(
                    id,
                    newDate,
                    newDescription,
                    newReference,
                    swapDebitCredit,
                    currentUserId,
                    cancellationToken);
                TempData["SuccessMessage"] = $"سند کپی شد. سند جدید: {voucher.DocumentNumber}";
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
        public async Task<IActionResult> PostVoucher(int id, CancellationToken cancellationToken)
        {
            var currentUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            try
            {
                var voucher = await _ledgerService.PostVoucherAsync(id, currentUserId, cancellationToken);
                TempData["SuccessMessage"] = $"سند {voucher.VoucherNumber} با موفقیت ثبت قطعی شد.";
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
        public async Task<IActionResult> ReverseVoucher(int id, string? reason, DateTime? reversalDate, CancellationToken cancellationToken)
        {
            try
            {
                var voucher = await _ledgerService.ReverseVoucherAsync(id, reason, reversalDate, cancellationToken);
                TempData["SuccessMessage"] = $"سند برگشتی ایجاد شد: {voucher.DocumentNumber}";
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
        public async Task<IActionResult> VoidVoucher(int id, string? reason, CancellationToken cancellationToken)
        {
            try
            {
                var voucher = await _ledgerService.VoidVoucherAsync(id, reason, cancellationToken);
                TempData["SuccessMessage"] = $"سند {voucher.DocumentNumber} ابطال شد.";
            }
            catch (InvalidOperationException ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }

            return RedirectToAction(nameof(LedgerOperations));
        }

        [HttpGet]
        [PermissionAuthorize("Finance.View")]
        public async Task<IActionResult> Ledger(
            DateTime? from,
            DateTime? to,
            int? subsidiaryAccountId,
            int? generalAccountId,
            int? detailedAccountId,
            Guid? floatingDetailAccountId,
            bool includeDraft = false,
            string? format = null,
            CancellationToken cancellationToken = default)
        {
            var report = await _ledgerReportService.GetLedgerAsync(new LedgerRequest
            {
                From = from,
                To = to,
                SubsidiaryAccountId = subsidiaryAccountId,
                GeneralAccountId = generalAccountId,
                DetailedAccountId = detailedAccountId,
                FloatingDetailAccountId = floatingDetailAccountId,
                IncludeDraft = includeDraft
            }, cancellationToken);

            if (string.Equals(format, "json", StringComparison.OrdinalIgnoreCase))
            {
                return Json(report);
            }

            ViewBag.From = from?.Date;
            ViewBag.To = to?.Date;
            ViewBag.SubsidiaryAccountId = subsidiaryAccountId;
            ViewBag.GeneralAccountId = generalAccountId;
            ViewBag.DetailedAccountId = detailedAccountId;
            ViewBag.FloatingDetailAccountId = floatingDetailAccountId;
            ViewBag.IncludeDraft = includeDraft;
            return View("Ledger", report);
        }

        private async Task<IActionResult> VatDashboardInternal(int? year, CancellationToken cancellationToken)
        {
            var targetYear = year ?? FinancialAppService.GetCurrentShamsiYear();
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
            var targetYear = year ?? FinancialAppService.GetCurrentShamsiYear();
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
            var items = await _appService.GetInvoicesForExportAsync(invoiceType, year, quarter, cancellationToken);

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
            var items = await _inventoryService.GetInventoryStocksAsync(warehouseId, cancellationToken);

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
                worksheet.Cell(row, 1).Value = items[index].ProductCode;
                worksheet.Cell(row, 2).Value = items[index].ProductName;
                worksheet.Cell(row, 3).Value = items[index].WarehouseName;
                worksheet.Cell(row, 4).Value = items[index].Unit;
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
            var invoice = await _appService.GetInvoiceForPrintAsync(id, cancellationToken);
            if (invoice == null)
            {
                return NotFound();
            }

            return View("PrintInvoice", invoice);
        }

        [HttpGet]
        public async Task<IActionResult> PrintWarehouseReceiptPdf(int id, CancellationToken cancellationToken)
        {
            var receipt = await _appService.GetWarehouseReceiptForPrintAsync(id, cancellationToken);
            if (receipt == null)
            {
                return NotFound();
            }

            return View("PrintWarehouseReceipt", receipt);
        }

        [HttpGet]
        public async Task<IActionResult> PrintPayslipPdf(int payrollListId, int payrollItemId, CancellationToken cancellationToken)
        {
            var (payroll, payrollItem) = await _appService.GetPayslipForPrintAsync(payrollListId, payrollItemId, cancellationToken);

            if (payroll == null || payrollItem == null)
            {
                return NotFound();
            }

            ViewBag.PayrollYear = payroll.Year;
            ViewBag.PayrollMonth = payroll.Month;
            return View("PrintPayslip", payrollItem);
        }

        private async Task PopulateInvoiceDropdownsAsync(FinancialInvoiceUpsertVM model, CancellationToken cancellationToken)
        {
            await _appService.PopulateProductOptionsAsync(model.ProductOptions, cancellationToken);
            await _appService.PopulatePurchaseOptionsAsync(model, cancellationToken);
            await _appService.PopulateEmployerOptionsAsync(model.EmployerOptions, cancellationToken);
        }
    }
}