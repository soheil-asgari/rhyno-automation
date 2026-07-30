using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeAutomation.DTOs.Api.v1;
using OfficeAutomation.Models;
using OfficeAutomation.Modules.Finance.Application;
using OfficeAutomation.Modules.Finance.Infrastructure.Persistence;
using OfficeAutomation.Services;
using OfficeAutomation.Services.Security;

namespace OfficeAutomation.Controllers.Api.v1;

/// <summary>
/// کنترلر API ماژول مالی - مدیریت اسناد حسابداری و گزارشات مالی
/// </summary>
[ApiController]
[Route("api/v1/finance")]
[Authorize(AuthenticationSchemes = ApiKeyAuthenticationExtensions.AuthenticationScheme)]
[Produces("application/json")]
public sealed class FinanceApiController : ApiBaseController
{
    private readonly FinanceDbContext _context;
    private readonly WorkflowService _workflowService;
    private readonly FinanceLedgerService _ledgerService;
    private readonly FinancialInvoiceService _invoiceService;

    /// <summary>
    /// سازنده کنترلر API مالی
    /// </summary>
    public FinanceApiController(
        FinanceDbContext context,
        WorkflowService workflowService,
        FinanceLedgerService ledgerService,
        FinancialInvoiceService invoiceService)
    {
        _context = context;
        _workflowService = workflowService;
        _ledgerService = ledgerService;
        _invoiceService = invoiceService;
    }

    /// <summary>
    /// دریافت لیست اسناد حسابداری با صفحه‌بندی و فیلتر تاریخ/وضعیت
    /// </summary>
    /// <param name="filter">فیلترهای جستجو شامل تاریخ، وضعیت و صفحه‌بندی</param>
    /// <param name="cancellationToken">توکن لغو درخواست</param>
    /// <returns>لیست صفحه‌بندی شده اسناد حسابداری</returns>
    [HttpGet("vouchers")]
    [ProducesResponseType(typeof(ApiPagedResponse<VoucherSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetVouchers(
        [FromQuery] VoucherFilterRequest filter,
        CancellationToken cancellationToken)
    {
        var query = _context.Invoices
            .AsNoTracking()
            .AsQueryable();

        if (filter.FromDate.HasValue)
        {
            query = query.Where(i => i.InvoiceDate >= filter.FromDate.Value);
        }

        if (filter.ToDate.HasValue)
        {
            query = query.Where(i => i.InvoiceDate <= filter.ToDate.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            query = query.Where(i => i.WorkflowStatus == filter.Status.Trim());
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim();
            query = query.Where(i =>
                i.InvoiceNumber.Contains(search) ||
                i.PartyName.Contains(search));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(i => i.CreatedAt)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(i => VoucherSummaryDto.From(i))
            .ToListAsync(cancellationToken);

        return ApiPaged(items, totalCount, filter.Page, filter.PageSize);
    }

    /// <summary>
    /// دریافت جزئیات یک سند حسابداری
    /// </summary>
    /// <param name="id">شناسه یکتای سند</param>
    /// <param name="cancellationToken">توکن لغو درخواست</param>
    /// <returns>جزئیات کامل سند حسابداری</returns>
    [HttpGet("vouchers/{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<VoucherDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetVoucher(
        [FromRoute] int id,
        CancellationToken cancellationToken)
    {
        var invoice = await _context.Invoices
            .AsNoTracking()
            .Include(i => i.Items)
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

        if (invoice == null)
        {
            return ApiError("سند حسابداری مورد نظر یافت نشد.", StatusCodes.Status404NotFound);
        }

        return ApiOk(VoucherDetailDto.From(invoice));
    }

    /// <summary>
    /// ایجاد سند حسابداری جدید (فاکتور فروش یا خرید)
    /// </summary>
    /// <param name="request">اطلاعات سند جدید شامل نوع، تاریخ، طرف حساب و اقلام</param>
    /// <param name="cancellationToken">توکن لغو درخواست</param>
    /// <returns>سند حسابداری ایجاد شده</returns>
    [HttpPost("vouchers")]
    [ProducesResponseType(typeof(ApiResponse<VoucherDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateVoucher(
        [FromBody] CreateVoucherRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();
            return ApiError("اطلاعات ورودی نامعتبر است.", StatusCodes.Status400BadRequest, errors);
        }

        var calculation = _invoiceService.CalculateTotals(request.Items.Select(i => new FinancialInvoiceItemVM
        {
            ProductId = i.ProductId,
            ItemName = i.ItemName,
            Quantity = i.Quantity,
            UnitPrice = i.UnitPrice,
            LineSubTotal = Math.Round(i.Quantity * i.UnitPrice, 2),
            LineVatAmount = Math.Round(i.Quantity * i.UnitPrice * 0.10m, 2),
            LineGrandTotal = Math.Round(i.Quantity * i.UnitPrice * 1.10m, 2)
        }).ToList());

        var entity = new Invoice
        {
            InvoiceNumber = request.InvoiceNumber.Trim(),
            InvoiceType = request.InvoiceType,
            DateShamsi = request.DateShamsi,
            PartyName = request.PartyName.Trim(),
            NationalCodeOrEconomicId = request.NationalCodeOrEconomicId?.Trim(),
            SubTotal = calculation.SubTotal,
            VatAmount = calculation.VatAmount,
            GrandTotal = calculation.GrandTotal,
            Notes = request.Notes?.Trim(),
            Amount = calculation.GrandTotal,
            VendorName = request.PartyName.Trim(),
            InvoiceDate = DateTime.Now,
            WorkflowStatus = WorkflowStatus.Sent,
            WarehouseReceiptId = request.InvoiceType == "Purchase" ? request.WarehouseReceiptId : null,
            EmployerId = request.InvoiceType == "Sale" ? request.EmployerId : null,
            CreatedAt = DateTime.Now,
            CreatedByUserId = CurrentUserId,
            Items = calculation.ValidItems.Select(i => new InvoiceItem
            {
                ProductId = i.ProductId,
                ItemName = i.ItemName.Trim(),
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                LineSubTotal = i.LineSubTotal,
                LineVatAmount = i.LineVatAmount,
                LineGrandTotal = i.LineGrandTotal
            }).ToList()
        };

        _context.Invoices.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);

        await _workflowService.StartRoutingAsync(
            "Invoice",
            CurrentUserId ?? string.Empty,
            CurrentUserId ?? string.Empty,
            entity.Id);

        return ApiOk(VoucherDetailDto.From(entity), "سند حسابداری با موفقیت ایجاد شد.");
    }

    /// <summary>
    /// دریافت گزارش ترازنامه / خلاصه وضعیت مالی
    /// </summary>
    /// <param name="year">سال مالی (اختیاری - پیش‌فرض سال جاری شمسی)</param>
    /// <param name="cancellationToken">توکن لغو درخواست</param>
    /// <returns>گزارش خلاصه وضعیت مالی</returns>
    [HttpGet("reports/balance")]
    [ProducesResponseType(typeof(ApiResponse<BalanceReportDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetBalanceReport(
        [FromQuery] int? year,
        CancellationToken cancellationToken)
    {
        var targetYear = year ?? GetCurrentShamsiYear();

        var invoices = await _context.Invoices
            .AsNoTracking()
            .Where(i => i.DateShamsi.StartsWith(targetYear.ToString()))
            .ToListAsync(cancellationToken);

        var totalRevenue = invoices.Where(i => i.InvoiceType == "Sale").Sum(i => i.GrandTotal);
        var totalPurchaseCost = invoices.Where(i => i.InvoiceType == "Purchase").Sum(i => i.GrandTotal);
        var pendingSales = invoices.Count(i => i.InvoiceType == "Sale" && i.WorkflowStatus != WorkflowStatus.Approved && i.WorkflowStatus != WorkflowStatus.Archived);
        var pendingPurchase = invoices.Count(i => i.InvoiceType == "Purchase" && i.WorkflowStatus != WorkflowStatus.Approved && i.WorkflowStatus != WorkflowStatus.Archived);
        var vatCollected = invoices.Where(i => i.InvoiceType == "Sale").Sum(i => i.VatAmount);
        var vatPaid = invoices.Where(i => i.InvoiceType == "Purchase").Sum(i => i.VatAmount);

        var report = new BalanceReportDto
        {
            Year = targetYear,
            TotalRevenue = Math.Round(totalRevenue, 2),
            TotalCosts = Math.Round(totalPurchaseCost, 2),
            NetProfitLoss = Math.Round(totalRevenue - totalPurchaseCost, 2),
            PendingSalesInvoices = pendingSales,
            PendingPurchaseInvoices = pendingPurchase,
            TotalVatCollected = Math.Round(vatCollected, 2),
            TotalVatPaid = Math.Round(vatPaid, 2)
        };

        return ApiOk(report, "گزارش ترازنامه با موفقیت دریافت شد.");
    }

    private static int GetCurrentShamsiYear()
    {
        var persianCalendar = new System.Globalization.PersianCalendar();
        return persianCalendar.GetYear(DateTime.Now);
    }
}