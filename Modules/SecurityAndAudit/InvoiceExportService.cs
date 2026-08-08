using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OfficeAutomation.Modules.Persistence;

namespace OfficeAutomation.Modules.SecurityAndAudit;

/// <inheritdoc />
public sealed class InvoiceExportService : IInvoiceExportService
{
    private readonly IDbContextFactory<TaxDbContext> _dbFactory;
    private readonly ILogger<InvoiceExportService> _logger;

    public InvoiceExportService(
        IDbContextFactory<TaxDbContext> dbFactory,
        ILogger<InvoiceExportService> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    public async Task<byte[]> ExportInvoicesAsync(InvoiceExportFilter? filter, CancellationToken cancellationToken)
    {
        using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var query = db.InvoiceRecords
            .Include(r => r.Seller)
            .Include(r => r.Buyer)
            .AsNoTracking()
            .AsQueryable();

        if (filter is not null)
        {
            if (!string.IsNullOrWhiteSpace(filter.InvoiceNumber))
            {
                query = query.Where(r => r.InvoiceNumber.Contains(filter.InvoiceNumber));
            }

            if (!string.IsNullOrWhiteSpace(filter.SellerName))
            {
                query = query.Where(r => r.Seller != null && r.Seller.Name.Contains(filter.SellerName));
            }

            if (!string.IsNullOrWhiteSpace(filter.BuyerName))
            {
                query = query.Where(r => r.Buyer != null && r.Buyer.Name.Contains(filter.BuyerName));
            }

            if (!string.IsNullOrWhiteSpace(filter.InvoiceType))
            {
                query = query.Where(r => r.InvoiceType == filter.InvoiceType);
            }

            if (!string.IsNullOrWhiteSpace(filter.Status))
            {
                query = query.Where(r => r.Status == filter.Status);
            }

            if (filter.FromDate.HasValue)
            {
                query = query.Where(r => r.InvoiceDate >= filter.FromDate.Value);
            }

            if (filter.ToDate.HasValue)
            {
                query = query.Where(r => r.InvoiceDate <= filter.ToDate.Value);
            }
        }

        var invoices = await query
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("فاکتورها");

        string[] headers =
        {
            "شماره فاکتور", "تاریخ (شمسی)", "فروشنده", "خریدار", "نوع",
            "جمع خالص", "تخفیف", "مالیات", "عوارض", "قابل پرداخت", "وضعیت"
        };

        WriteHeader(sheet, headers);

        var row = 2;
        foreach (var inv in invoices)
        {
            sheet.Cell(row, 1).Value = inv.InvoiceNumber;
            sheet.Cell(row, 2).Value = inv.DateShamsi;
            sheet.Cell(row, 3).Value = inv.Seller?.Name ?? "-";
            sheet.Cell(row, 4).Value = inv.Buyer?.Name ?? "-";
            sheet.Cell(row, 5).Value = inv.InvoiceType;
            sheet.Cell(row, 6).Value = inv.TotalAmount;
            sheet.Cell(row, 7).Value = inv.DiscountAmount;
            sheet.Cell(row, 8).Value = inv.VatAmount;
            sheet.Cell(row, 9).Value = inv.DutyAmount;
            sheet.Cell(row, 10).Value = inv.FinalAmount;
            sheet.Cell(row, 11).Value = inv.Status;
            row++;
        }

        sheet.Columns().AdjustToContents();
        _logger.LogInformation("Exported {Count} invoice records to Excel.", invoices.Count);
        return ToBytes(workbook);
    }
public async Task<byte[]> ExportTaxBreakdownsAsync(CancellationToken cancellationToken)
    {
        using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var logs = await db.TaxLogs
            .Include(t => t.InvoiceRecord)
            .AsNoTracking()
            .OrderByDescending(t => t.CalculatedAt)
            .Take(5000)
            .ToListAsync(cancellationToken);

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("محاسبات مالیات");

        string[] headers =
        {
            "شماره فاکتور", "مشمول مالیات", "معاف", "نرخ مالیات (%)",
            "مالیات", "عوارض", "قابل پرداخت", "تاریخ محاسبه"
        };

        WriteHeader(sheet, headers);

        var row = 2;
        foreach (var log in logs)
        {
            sheet.Cell(row, 1).Value = log.InvoiceRecord?.InvoiceNumber ?? string.Empty;
            sheet.Cell(row, 2).Value = log.TaxableAmount;
            sheet.Cell(row, 3).Value = log.ExemptAmount;
            sheet.Cell(row, 4).Value = log.VatRate * 100m;
            sheet.Cell(row, 5).Value = log.TaxAmount;
            sheet.Cell(row, 6).Value = log.DutyAmount;
            sheet.Cell(row, 7).Value = log.FinalAmount;
            sheet.Cell(row, 8).Value = log.CalculatedAt.ToString("yyyy/MM/dd HH:mm");
            row++;
        }

        sheet.Columns().AdjustToContents();
        _logger.LogInformation("Exported {Count} tax breakdown rows to Excel.", logs.Count);
        return ToBytes(workbook);
    }

    public async Task<byte[]> ExportMowadiyanSubmissionsAsync(CancellationToken cancellationToken)
    {
        using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var submissions = await db.MowadiyanSubmissions
            .Include(s => s.InvoiceRecord)
            .AsNoTracking()
            .OrderByDescending(s => s.CreatedAt)
            .Take(5000)
            .ToListAsync(cancellationToken);

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("ارسال‌های مودیان");

        string[] headers =
        {
            "شماره فاکتور", "وضعیت", "شماره پیگیری", "شناسه مالیاتی",
            "تعداد تلاش", "خطا", "تاریخ ارسال"
        };

        WriteHeader(sheet, headers);

        var row = 2;
        foreach (var s in submissions)
        {
            sheet.Cell(row, 1).Value = s.InvoiceRecord?.InvoiceNumber ?? s.InvoiceRecordId.ToString("N");
            sheet.Cell(row, 2).Value = s.StatusText;
            sheet.Cell(row, 3).Value = s.TaxReferenceId ?? "-";
            sheet.Cell(row, 4).Value = s.FiscalId ?? "-";
            sheet.Cell(row, 5).Value = s.RetryCount;
            sheet.Cell(row, 6).Value = s.ErrorMessage ?? "-";
            sheet.Cell(row, 7).Value = s.SubmittedAt?.ToString("yyyy/MM/dd HH:mm") ?? "-";
            row++;
        }

        sheet.Columns().AdjustToContents();
        _logger.LogInformation("Exported {Count} Mowadiyan submissions to Excel.", submissions.Count);
        return ToBytes(workbook);
    }

    private static void WriteHeader(IXLWorksheet sheet, string[] headers)
    {
        for (int i = 0; i < headers.Length; i++)
        {
            var cell = sheet.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#2D3446");
            cell.Style.Font.FontColor = XLColor.White;
        }
    }

    private static byte[] ToBytes(XLWorkbook workbook)
    {
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}