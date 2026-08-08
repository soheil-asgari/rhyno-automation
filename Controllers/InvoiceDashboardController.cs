using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Filters;
using OfficeAutomation.Modules.InvoiceStructuring;
using OfficeAutomation.Modules.Persistence;
using OfficeAutomation.Modules.Persistence.Entities;
using OfficeAutomation.Modules.TaxEngine;
using OfficeAutomation.Utilities;

namespace OfficeAutomation.Controllers;

[Authorize]
[RequireAccessArea("Finance")]
public class InvoiceDashboardController : Controller
{
    private readonly IDbContextFactory<TaxDbContext> _dbFactory;
    private readonly ITaxCalculatorService _taxCalculator;
    private readonly ILogger<InvoiceDashboardController> _logger;

    public InvoiceDashboardController(
        IDbContextFactory<TaxDbContext> dbFactory,
        ITaxCalculatorService taxCalculator,
        ILogger<InvoiceDashboardController> logger)
    {
        _dbFactory = dbFactory;
        _taxCalculator = taxCalculator;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        using var db = await _dbFactory.CreateDbContextAsync();

        var totalProcessed = await db.InvoiceRecords
            .CountAsync(r => r.Status == "Completed" || r.Status == "Processed");

        var totalTaxLiability = await db.InvoiceRecords
            .Where(r => r.Status == "Completed" || r.Status == "Processed")
            .SumAsync(r => r.VatAmount);

        var totalRevenue = await db.InvoiceRecords
            .Where(r => r.InvoiceType == "Sale" && (r.Status == "Completed" || r.Status == "Processed"))
            .SumAsync(r => r.FinalAmount);

        var totalExpense = await db.InvoiceRecords
            .Where(r => r.InvoiceType == "Purchase" && (r.Status == "Completed" || r.Status == "Processed"))
            .SumAsync(r => r.FinalAmount);

        var recentInvoices = await db.InvoiceRecords
            .OrderByDescending(r => r.CreatedAt)
            .Take(20)
            .ToListAsync();

        ViewBag.TotalProcessed = totalProcessed;
        ViewBag.TotalTaxLiability = totalTaxLiability;
        ViewBag.TotalRevenue = totalRevenue;
        ViewBag.TotalExpense = totalExpense;
        ViewBag.RecentInvoices = recentInvoices;

        return Request.IsRhynoTabOrAjax() ? PartialView() : View();
    }

    [HttpGet]
    public async Task<IActionResult> Search(
        string? query = null, string? dateFrom = null, string? dateTo = null,
        string? sellerFilter = null, int page = 1, int pageSize = 25)
    {
        using var db = await _dbFactory.CreateDbContextAsync();
        var filtered = db.InvoiceRecords
            .Include(r => r.Seller).Include(r => r.Buyer).AsQueryable();

        if (!string.IsNullOrWhiteSpace(query))
        {
            var q = query.Trim();
            filtered = filtered.Where(r =>
                (r.RawOcrText != null && r.RawOcrText.Contains(q)) ||
                r.InvoiceNumber.Contains(q) ||
                (r.Seller != null && r.Seller.Name.Contains(q)) ||
                (r.Buyer != null && r.Buyer.Name.Contains(q)));
        }
        if (!string.IsNullOrWhiteSpace(dateFrom))
        {
            var from = JalaliDateHelper.ParseShamsiToGregorian(dateFrom);
            if (from.HasValue) filtered = filtered.Where(r => r.InvoiceDate >= from.Value);
        }
        if (!string.IsNullOrWhiteSpace(dateTo))
        {
            var to = JalaliDateHelper.ParseShamsiToGregorian(dateTo);
            if (to.HasValue) filtered = filtered.Where(r => r.InvoiceDate <= to.Value);
        }
        if (!string.IsNullOrWhiteSpace(sellerFilter))
            filtered = filtered.Where(r => r.Seller != null && r.Seller.Name.Contains(sellerFilter));

        var totalCount = await filtered.CountAsync();
        var items = await filtered.OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(r => new InvoiceDashboardItemDto
            {
                Id = r.Id, InvoiceNumber = r.InvoiceNumber, DateShamsi = r.DateShamsi,
                SellerName = r.Seller != null ? r.Seller.Name : "-",
                BuyerName = r.Buyer != null ? r.Buyer.Name : "-",
                TotalAmount = r.TotalAmount, VatAmount = r.VatAmount,
                FinalAmount = r.FinalAmount, Status = r.Status, CreatedAt = r.CreatedAt
            }).ToListAsync();

        return Json(new
        {
            success = true, items, totalCount, page, pageSize,
            totalPages = (int)Math.Ceiling((double)totalCount / pageSize)
        });
    }
    [HttpGet]
    public async Task<IActionResult> Detail(Guid id)
    {
        using var db = await _dbFactory.CreateDbContextAsync();
        var invoice = await db.InvoiceRecords
            .Include(r => r.Seller).Include(r => r.Buyer)
            .Include(r => r.LineItems.OrderBy(li => li.DisplayOrder))
            .Include(r => r.TaxLogs.OrderByDescending(tl => tl.CalculatedAt))
            .FirstOrDefaultAsync(r => r.Id == id);
        if (invoice is null) return NotFound();
        return Request.IsRhynoTabOrAjax() ? PartialView(invoice) : View(invoice);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RecalculateTax(Guid id)
    {
        using var db = await _dbFactory.CreateDbContextAsync();
        var invoice = await db.InvoiceRecords
            .Include(r => r.LineItems).FirstOrDefaultAsync(r => r.Id == id);
        if (invoice is null)
            return Json(new { success = false, message = "فاکتور یافت نشد." });

        try
        {
            var breakdown = await _taxCalculator.CalculateSimpleAsync(
                totalBeforeTax: invoice.LineItems.Where(li => li.Kind == "Item").Sum(li => li.LineAmount),
                totalDiscount: invoice.LineItems.Where(li => li.Kind == "Discount").Sum(li => li.LineAmount),
                exemptItemAmount: invoice.LineItems.Where(li => li.IsTaxExempt).Sum(li => li.LineAmount),
                extraDuty: invoice.LineItems.Where(li => li.Kind == "Duty").Sum(li => li.LineAmount),
                extraShipping: invoice.LineItems.Where(li => li.Kind == "Shipping").Sum(li => li.LineAmount),
                extraInsurance: invoice.LineItems.Where(li => li.Kind == "Insurance").Sum(li => li.LineAmount),
                vatRate: invoice.VatRate);

            invoice.VatAmount = breakdown.TaxAmount;
            invoice.DutyAmount = breakdown.DutyAmount;
            invoice.FinalAmount = breakdown.FinalAmount;
            invoice.DiscountAmount = breakdown.TotalDiscount;
            invoice.SubTotal = breakdown.TotalBeforeTax;
            invoice.UpdatedAt = DateTime.Now;
            invoice.VatRate = breakdown.VatRate;

            var taxLog = new TaxLog
            {
                Id = Guid.NewGuid(), InvoiceRecordId = invoice.Id,
                TotalBeforeTax = breakdown.TotalBeforeTax,
                TotalDiscount = breakdown.TotalDiscount,
                TaxableAmount = breakdown.TaxableAmount,
                ExemptAmount = breakdown.ExemptAmount,
                VatRate = breakdown.VatRate,
                TaxAmount = breakdown.TaxAmount,
                DutyAmount = breakdown.DutyAmount,
                FinalAmount = breakdown.FinalAmount,
                HasExemptItems = breakdown.HasExemptItems,
                IsFullyExempt = breakdown.IsFullyExempt,
                SummaryPersian = breakdown.SummaryPersian,
                CalculatedBy = ResolveOwnerKey(),
                CalculatedAt = DateTime.Now
            };
            db.TaxLogs.Add(taxLog);
            await db.SaveChangesAsync();
            _logger.LogInformation("Tax recalculated for invoice {Id}", id);
            return Json(new { success = true, message = "محاسبه مالیات با موفقیت انجام شد.", breakdown });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tax recalculation failed for invoice {Id}", id);
            return Json(new { success = false, message = $"خطا در محاسبه مالیات: {ex.Message}" });
        }
    }

    private string ResolveOwnerKey() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "system";
}

public sealed class InvoiceDashboardItemDto
{
    public Guid Id { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public string DateShamsi { get; set; } = string.Empty;
    public string SellerName { get; set; } = string.Empty;
    public string BuyerName { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public decimal VatAmount { get; set; }
    public decimal FinalAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

