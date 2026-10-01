using OfficeAutomation.Services.Licensing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OfficeAutomation.Filters;
using OfficeAutomation.Services.Financial;
using OfficeAutomation.Services.Security;

namespace OfficeAutomation.Controllers;

[Authorize]
[RequireAccessArea("Finance")]
[RequiresModule(LicensableModules.Financial)]
public class AccountReviewController : Controller
{
    private readonly IAccountReviewService _reviewService;

    public AccountReviewController(IAccountReviewService reviewService)
    {
        _reviewService = reviewService;
    }

    [HttpGet]
    [PermissionAuthorize("Finance.View")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        ViewData["Title"] = "مرور حساب‌ها";
        ViewBag.FiscalYears = await _reviewService.GetFiscalYearsAsync(cancellationToken);
        ViewBag.JournalTypes = await _reviewService.GetJournalTypesAsync(cancellationToken);
        ViewBag.DetailedTypes = await _reviewService.GetDetailedTypesAsync(cancellationToken);
        ViewBag.VoucherStates = await _reviewService.GetVoucherStatesAsync(cancellationToken);
        return View("~/Views/Financial/AccountReview.cshtml");
    }

    [HttpGet]
    [PermissionAuthorize("Finance.View")]
    public async Task<IActionResult> Transactions(
        int level,
        string code,
        int fromFiscalYear,
        int toFiscalYear,
        string? voucherTypes,
        CancellationToken cancellationToken)
    {
        var filters = new AccountReviewRequest
        {
            Level = level,
            FromFiscalYear = fromFiscalYear,
            ToFiscalYear = toFiscalYear,
            VoucherTypes = ParseIntList(voucherTypes),
        };
        var result = await _reviewService.GetTransactionsAsync(level, code, filters, cancellationToken);
        return Json(result);
    }

    [HttpGet]
    [PermissionAuthorize("Finance.View")]
    public async Task<IActionResult> Children(
        int level,
        string parentCode,
        int fromFiscalYear,
        int toFiscalYear,
        int? accountType,
        int? detailType,
        string? voucherTypes,
        string? voucherStates,
        string? filter,
        int? fromMonth,
        int? toMonth,
        DateTime? fromDate,
        DateTime? toDate,
        int? fromNumber,
        int? toNumber,
        bool withBalance,
        CancellationToken cancellationToken)
    {
        var request = new AccountReviewRequest
        {
            Level = level,
            FromFiscalYear = fromFiscalYear,
            ToFiscalYear = toFiscalYear,
            AccountType = accountType,
            DetailType = detailType,
            VoucherTypes = ParseIntList(voucherTypes),
            VoucherStates = ParseIntList(voucherStates),
            Filter = string.IsNullOrWhiteSpace(filter) ? "month" : filter,
            FromMonth = fromMonth,
            ToMonth = toMonth,
            FromDate = fromDate,
            ToDate = toDate,
            FromNumber = fromNumber,
            ToNumber = toNumber,
            WithBalance = withBalance
        };
        var result = await _reviewService.GetChildAccountsAsync(request, parentCode, cancellationToken);
        return Json(result);
    }

    [HttpGet]
    [PermissionAuthorize("Finance.View")]
    public async Task<IActionResult> SourceDocument(int voucherLineId, CancellationToken cancellationToken)
    {
        var result = await _reviewService.GetSourceDocumentAsync(voucherLineId, cancellationToken);
        if (result is null)
        {
            return NotFound();
        }
        return Json(result);
    }

    [HttpGet]
    [PermissionAuthorize("Finance.View")]
    public async Task<IActionResult> Options(CancellationToken cancellationToken)
    {
        return Json(new
        {
            detailedTypes = await _reviewService.GetDetailedTypesAsync(cancellationToken),
            voucherStates = await _reviewService.GetVoucherStatesAsync(cancellationToken)
        });
    }

    [HttpGet]
    [PermissionAuthorize("Finance.View")]
    public async Task<IActionResult> Data(
        int level,
        int fromFiscalYear,
        int toFiscalYear,
        int? accountType,
        int? detailType,
        string? voucherTypes,
        string? voucherStates,
        string? filter,
        int? fromMonth,
        int? toMonth,
        DateTime? fromDate,
        DateTime? toDate,
        int? fromNumber,
        int? toNumber,
        bool withBalance,
        CancellationToken cancellationToken)
    {
        var request = new AccountReviewRequest
        {
            Level = level,
            FromFiscalYear = fromFiscalYear,
            ToFiscalYear = toFiscalYear,
            AccountType = accountType,
            DetailType = detailType,
            VoucherTypes = ParseIntList(voucherTypes),
            VoucherStates = ParseIntList(voucherStates),
            Filter = string.IsNullOrWhiteSpace(filter) ? "month" : filter,
            FromMonth = fromMonth,
            ToMonth = toMonth,
            FromDate = fromDate,
            ToDate = toDate,
            FromNumber = fromNumber,
            ToNumber = toNumber,
            WithBalance = withBalance
        };

        var result = await _reviewService.GetAccountReviewAsync(request, cancellationToken);
        return Json(result);
    }

    private static List<int> ParseIntList(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => int.TryParse(part, out var id) ? id : (int?)null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToList();
    }
}
