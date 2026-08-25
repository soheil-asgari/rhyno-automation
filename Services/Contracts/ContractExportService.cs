using System.Data;
using System.Text;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;
using OfficeAutomation.Models;
using OfficeAutomation.Modules.Office.Infrastructure.Persistence;

namespace OfficeAutomation.Services.Contracts;

public class ContractExportService : IContractExportService
{
    private readonly OfficeDbContext _db;
    private readonly IContractReportService _reportService;
    private readonly ILogger<ContractExportService> _logger;

    public ContractExportService(OfficeDbContext db, IContractReportService reportService, ILogger<ContractExportService> logger)
    {
        _db = db;
        _reportService = reportService;
        _logger = logger;
    }

    public async Task<byte[]> ExportContractsToExcelAsync(ContractIndexVM filters, string currentUserId, CancellationToken ct = default)
    {
        var query = _db.Contracts.Include(c => c.AssignedUser).AsNoTracking();

        if (!filters.ShowArchived) query = query.Where(c => c.Status != ContractStatuses.Archived);
        if (!string.IsNullOrWhiteSpace(filters.SearchTerm))
        { var t = filters.SearchTerm.Trim(); query = query.Where(c => c.ContractNumber.Contains(t) || c.Title.Contains(t) || c.ClientName!.Contains(t)); }
        if (!string.IsNullOrWhiteSpace(filters.Status)) query = query.Where(c => c.Status == filters.Status);
        if (!string.IsNullOrWhiteSpace(filters.ContractType)) query = query.Where(c => c.ContractType == filters.ContractType);
        if (!string.IsNullOrWhiteSpace(filters.AssignedUserId)) query = query.Where(c => c.AssignedUserId == filters.AssignedUserId);
        if (!string.IsNullOrWhiteSpace(filters.ClientName)) query = query.Where(c => c.ClientName!.Contains(filters.ClientName));
        if (filters.StartDateFrom.HasValue) query = query.Where(c => c.StartDate >= filters.StartDateFrom.Value);
        if (filters.StartDateTo.HasValue) query = query.Where(c => c.StartDate <= filters.StartDateTo.Value);
        if (filters.EndDateFrom.HasValue) query = query.Where(c => c.EndDate >= filters.EndDateFrom.Value);
        if (filters.EndDateTo.HasValue) query = query.Where(c => c.EndDate <= filters.EndDateTo.Value);
        if (filters.MinAmount.HasValue) query = query.Where(c => c.TotalAmountInclusiveTax >= filters.MinAmount.Value);
        if (filters.MaxAmount.HasValue) query = query.Where(c => c.TotalAmountInclusiveTax <= filters.MaxAmount.Value);
        if (filters.ExpiringSoonOnly == true) query = query.Where(c => c.EndDate.HasValue && c.EndDate.Value <= DateTime.UtcNow.AddMonths(3));

        var contracts = await query.OrderByDescending(c => c.CreatedAt)
            .Select(c => new { c.ContractNumber, c.Title, c.ContractType, c.Status, c.TotalAmountInclusiveTax, c.StartDate, c.EndDate, AssignedTo = c.AssignedUser != null ? c.AssignedUser.FullName ?? c.AssignedUser.UserName : "", c.CreatedAt })
            .ToListAsync(ct);

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Contracts");
        var headers = new[] { "Contract Number", "Title", "Type", "Status", "Value", "Start Date", "End Date", "Assigned To", "Created Date" };
        for (int i = 0; i < headers.Length; i++) { ws.Cell(1, i + 1).Value = headers[i]; StyleHeader(ws.Cell(1, i + 1)); }
        for (int r = 0; r < contracts.Count; r++)
        {
            var c = contracts[r];
            ws.Cell(r + 2, 1).Value = c.ContractNumber; ws.Cell(r + 2, 2).Value = c.Title;
            ws.Cell(r + 2, 3).Value = c.ContractType; ws.Cell(r + 2, 4).Value = c.Status;
            ws.Cell(r + 2, 5).Value = c.TotalAmountInclusiveTax; ws.Cell(r + 2, 5).Style.NumberFormat.Format = "#,##0";
            ws.Cell(r + 2, 6).Value = c.StartDate.ToString("yyyy-MM-dd");
            ws.Cell(r + 2, 7).Value = c.EndDate?.ToString("yyyy-MM-dd") ?? "";
            ws.Cell(r + 2, 8).Value = c.AssignedTo; ws.Cell(r + 2, 9).Value = c.CreatedAt.ToString("yyyy-MM-dd");
        }
        ws.Columns().AdjustToContents();
        using var ms = new MemoryStream(); wb.SaveAs(ms); return ms.ToArray();
    }

    public async Task<byte[]> ExportReportToExcelAsync(string reportType, CancellationToken ct = default)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add(reportType);
        switch (reportType.ToLowerInvariant())
        {
            case "aging": await WriteAgingAsync(ws, ct); break;
            case "revenue": await WriteRevenueAsync(ws, ct); break;
            case "pipeline": await WritePipelineAsync(ws, ct); break;
            case "calendar": await WriteCalendarAsync(ws, ct); break;
            case "exposure": await WriteExposureAsync(ws, ct); break;
            default: throw new ArgumentException($"Unknown report type: {reportType}");
        }
        ws.Columns().AdjustToContents();
        using var ms = new MemoryStream(); wb.SaveAs(ms); return ms.ToArray();
    }

    private static void StyleHeader(IXLCell cell) { cell.Style.Font.Bold = true; cell.Style.Fill.BackgroundColor = XLColor.FromArgb(0x0F, 0x76, 0x6B); cell.Style.Font.FontColor = XLColor.White; }
private async Task WriteAgingAsync(IXLWorksheet ws, CancellationToken ct)
    {
        var d = await _reportService.GetPaymentAgingReportAsync(ct);
        ws.Cell(1, 1).Value = "Payment Aging"; ws.Cell(1, 1).Style.Font.Bold = true; ws.Cell(1, 1).Style.Font.FontSize = 14;
        ws.Cell(3, 1).Value = "Total Overdue"; ws.Cell(3, 2).Value = d.TotalOverdue; ws.Cell(3, 2).Style.NumberFormat.Format = "#,##0";
        ws.Cell(4, 1).Value = "Count"; ws.Cell(4, 2).Value = d.TotalOverdueCount;
        ws.Cell(5, 1).Value = "Average"; ws.Cell(5, 2).Value = d.AverageOverdueAmount; ws.Cell(5, 2).Style.NumberFormat.Format = "#,##0";
        ws.Cell(6, 1).Value = "Max Days"; ws.Cell(6, 2).Value = d.MaxDaysOverdue;
        ws.Cell(8, 1).Value = "Bucket"; ws.Cell(8, 2).Value = "Count"; ws.Cell(8, 3).Value = "Total"; ws.Row(8).Style.Font.Bold = true;
        for (int i = 0; i < d.Buckets.Count; i++) { var b = d.Buckets[i]; ws.Cell(9 + i, 1).Value = b.BucketName; ws.Cell(9 + i, 2).Value = b.Count; ws.Cell(9 + i, 3).Value = b.TotalAmount; ws.Cell(9 + i, 3).Style.NumberFormat.Format = "#,##0"; }
    }

    private async Task WriteRevenueAsync(IXLWorksheet ws, CancellationToken ct)
    {
        var d = await _reportService.GetRevenueRecognitionScheduleAsync(ct);
        ws.Cell(1, 1).Value = "Revenue Recognition"; ws.Cell(1, 1).Style.Font.Bold = true; ws.Cell(1, 1).Style.Font.FontSize = 14;
        ws.Cell(3, 1).Value = "Recognized"; ws.Cell(3, 2).Value = d.TotalRecognized; ws.Cell(3, 2).Style.NumberFormat.Format = "#,##0";
        ws.Cell(4, 1).Value = "Scheduled"; ws.Cell(4, 2).Value = d.TotalScheduled; ws.Cell(4, 2).Style.NumberFormat.Format = "#,##0";
        ws.Cell(5, 1).Value = "Active Value"; ws.Cell(5, 2).Value = d.TotalActiveValue; ws.Cell(5, 2).Style.NumberFormat.Format = "#,##0";
        ws.Cell(6, 1).Value = "Active Cnt"; ws.Cell(6, 2).Value = d.ActiveContractCount;
        ws.Cell(8, 1).Value = "Period"; ws.Cell(8, 2).Value = "Recognized"; ws.Cell(8, 3).Value = "Scheduled"; ws.Row(8).Style.Font.Bold = true;
        for (int i = 0; i < d.Periods.Count; i++) { var p = d.Periods[i]; ws.Cell(9 + i, 1).Value = p.PeriodLabel; ws.Cell(9 + i, 2).Value = p.RecognizedAmount; ws.Cell(9 + i, 2).Style.NumberFormat.Format = "#,##0"; ws.Cell(9 + i, 3).Value = p.ScheduledAmount; ws.Cell(9 + i, 3).Style.NumberFormat.Format = "#,##0"; }
    }

    private async Task WritePipelineAsync(IXLWorksheet ws, CancellationToken ct)
    {
        var d = await _reportService.GetContractValuePipelineAsync(ct);
        ws.Cell(1, 1).Value = "Value Pipeline"; ws.Cell(1, 1).Style.Font.Bold = true; ws.Cell(1, 1).Style.Font.FontSize = 14;
        ws.Cell(3, 1).Value = "Grand Total"; ws.Cell(3, 2).Value = d.GrandTotalValue; ws.Cell(3, 2).Style.NumberFormat.Format = "#,##0";
        ws.Cell(4, 1).Value = "Count"; ws.Cell(4, 2).Value = d.GrandTotalCount;
        ws.Cell(5, 1).Value = "Average"; ws.Cell(5, 2).Value = d.AverageContractValue; ws.Cell(5, 2).Style.NumberFormat.Format = "#,##0";
        ws.Cell(7, 1).Value = "Status"; ws.Cell(7, 2).Value = "Count"; ws.Cell(7, 3).Value = "Value"; ws.Cell(7, 4).Value = "Share %"; ws.Row(7).Style.Font.Bold = true;
        for (int i = 0; i < d.Items.Count; i++) { var it = d.Items[i]; ws.Cell(8 + i, 1).Value = it.Status; ws.Cell(8 + i, 2).Value = it.Count; ws.Cell(8 + i, 3).Value = it.TotalValue; ws.Cell(8 + i, 3).Style.NumberFormat.Format = "#,##0"; ws.Cell(8 + i, 4).Value = it.PercentageOfTotal; }
    }
private async Task WriteCalendarAsync(IXLWorksheet ws, CancellationToken ct)
    {
        var d = await _reportService.GetObligationsCalendarAsync(6, ct);
        ws.Cell(1, 1).Value = "Obligations Calendar"; ws.Cell(1, 1).Style.Font.Bold = true; ws.Cell(1, 1).Style.Font.FontSize = 14;
        ws.Cell(3, 1).Value = "Total Events"; ws.Cell(3, 2).Value = d.TotalEvents;
        ws.Cell(4, 1).Value = "Total Amount"; ws.Cell(4, 2).Value = d.TotalAmountDue; ws.Cell(4, 2).Style.NumberFormat.Format = "#,##0";
        ws.Cell(5, 1).Value = "Period"; ws.Cell(5, 2).Value = d.UpcomingMonths + "m";
        ws.Cell(7, 1).Value = "Date"; ws.Cell(7, 2).Value = "Type"; ws.Cell(7, 3).Value = "Count"; ws.Cell(7, 4).Value = "Amount"; ws.Row(7).Style.Font.Bold = true;
        for (int i = 0; i < d.Events.Count; i++) { var e = d.Events[i]; ws.Cell(8 + i, 1).Value = e.Date.ToString("yyyy-MM-dd"); ws.Cell(8 + i, 2).Value = e.ObligationTypeDisplay; ws.Cell(8 + i, 3).Value = e.Count; ws.Cell(8 + i, 4).Value = e.TotalAmount; ws.Cell(8 + i, 4).Style.NumberFormat.Format = "#,##0"; }
    }

    private async Task WriteExposureAsync(IXLWorksheet ws, CancellationToken ct)
    {
        var d = await _reportService.GetFinancialExposureAsync(ct);
        ws.Cell(1, 1).Value = "Financial Exposure"; ws.Cell(1, 1).Style.Font.Bold = true; ws.Cell(1, 1).Style.Font.FontSize = 14;
        ws.Cell(3, 1).Value = "Active Value"; ws.Cell(3, 2).Value = d.TotalActiveValue; ws.Cell(3, 2).Style.NumberFormat.Format = "#,##0";
        ws.Cell(4, 1).Value = "Active Cnt"; ws.Cell(4, 2).Value = d.ActiveContractCount;
        ws.Cell(5, 1).Value = "At Risk"; ws.Cell(5, 2).Value = d.AtRiskValue; ws.Cell(5, 2).Style.NumberFormat.Format = "#,##0";
        ws.Cell(6, 1).Value = "Risk Cnt"; ws.Cell(6, 2).Value = d.AtRiskCount;
        ws.Cell(7, 1).Value = "Pending"; ws.Cell(7, 2).Value = d.PendingApprovalValue; ws.Cell(7, 2).Style.NumberFormat.Format = "#,##0";
        ws.Cell(8, 1).Value = "Pending Cnt"; ws.Cell(8, 2).Value = d.PendingApprovalCount;
        ws.Cell(9, 1).Value = "Receivables"; ws.Cell(9, 2).Value = d.TotalOutstandingReceivables; ws.Cell(9, 2).Style.NumberFormat.Format = "#,##0";
        ws.Cell(10, 1).Value = "Recognized"; ws.Cell(10, 2).Value = d.TotalRecognizedRevenue; ws.Cell(10, 2).Style.NumberFormat.Format = "#,##0";
        ws.Cell(11, 1).Value = "Exposure Ratio"; ws.Cell(11, 2).Value = d.ExposureRatioPercent + "%";
        if (d.CurrencyBreakdown.Count > 0)
        {
            ws.Cell(13, 1).Value = "Currency"; ws.Cell(13, 2).Value = "Amount"; ws.Cell(13, 3).Value = "Cnt"; ws.Cell(13, 4).Value = "Share %"; ws.Row(13).Style.Font.Bold = true;
            for (int i = 0; i < d.CurrencyBreakdown.Count; i++) { var c = d.CurrencyBreakdown[i]; ws.Cell(14 + i, 1).Value = c.CurrencyCode; ws.Cell(14 + i, 2).Value = c.TotalAmount; ws.Cell(14 + i, 2).Style.NumberFormat.Format = "#,##0"; ws.Cell(14 + i, 3).Value = c.ContractCount; ws.Cell(14 + i, 4).Value = c.Percentage; }
        }
    }
public async Task<byte[]> ExportReportToPdfAsync(string reportType, CancellationToken ct = default)
    {
        var html = await GenerateReportHtmlAsync(reportType, ct);
        return await HtmlToPdfAsync(html, ct);
    }

    public async Task<string> GenerateReportHtmlAsync(string reportType, CancellationToken ct = default)
    {
        var css = @"<style>body{font-family:'Segoe UI',Tahoma,sans-serif;padding:20px;color:#1e293b;direction:rtl;}h1{font-size:18px;color:#0f766e;border-bottom:2px solid #0f766e;padding-bottom:6px;}h2{font-size:14px;color:#334155;margin-top:16px;}.kpi-grid{display:flex;gap:12px;flex-wrap:wrap;margin:12px 0;}.kpi-card{background:#f8fafc;border:1px solid #e2e8f0;border-radius:6px;padding:10px 14px;min-width:120px;}.kpi-label{font-size:10px;color:#64748b;}.kpi-value{font-size:16px;font-weight:bold;}table{width:100%;border-collapse:collapse;margin-top:8px;font-size:11px;}th{background:#f1f5f9;color:#475569;text-align:right;padding:6px 10px;border-bottom:1px solid #e2e8f0;font-size:10px;}td{padding:5px 10px;border-bottom:1px solid #f1f5f9;}.text-emerald{color:#059669;}.text-rose{color:#e11d48;}.text-sky{color:#0284c7;}.text-amber{color:#d97706;}.text-slate{color:#475569;}</style>";
        var sb = new StringBuilder();
        sb.Append("<!DOCTYPE html><html><head><meta charset=\"UTF-8\">").Append(css).Append("</head><body>");
        switch (reportType.ToLowerInvariant())
        {
            case "aging": await BuildAgingHtmlAsync(sb, ct); break;
            case "revenue": await BuildRevenueHtmlAsync(sb, ct); break;
            case "pipeline": await BuildPipelineHtmlAsync(sb, ct); break;
            case "calendar": await BuildCalendarHtmlAsync(sb, ct); break;
            case "exposure": await BuildExposureHtmlAsync(sb, ct); break;
            default: sb.Append("<p>Unknown report type.</p>"); break;
        }
        sb.Append("</body></html>");
        return sb.ToString();
    }
private async Task BuildAgingHtmlAsync(StringBuilder sb, CancellationToken ct)
    {
        var d = await _reportService.GetPaymentAgingReportAsync(ct);
        sb.Append("<h1>Payment Aging Report</h1><div class=\"kpi-grid\">");
        AppendKpi(sb, "Total Overdue", d.TotalOverdue.ToString("N0"), "rose"); AppendKpi(sb, "Count", d.TotalOverdueCount.ToString(), "slate");
        AppendKpi(sb, "Average", d.AverageOverdueAmount.ToString("N0"), "slate"); AppendKpi(sb, "Max Delay", d.MaxDaysOverdue + " days", "amber");
        sb.Append("</div><table><thead><tr><th>Bucket</th><th>Count</th><th>Total</th></tr></thead><tbody>");
        foreach (var b in d.Buckets) sb.AppendFormat("<tr><td>{0}</td><td>{1}</td><td>{2:N0}</td></tr>", b.BucketName, b.Count, b.TotalAmount);
        sb.Append("</tbody></table>");
    }

    private async Task BuildRevenueHtmlAsync(StringBuilder sb, CancellationToken ct)
    {
        var d = await _reportService.GetRevenueRecognitionScheduleAsync(ct);
        sb.Append("<h1>Revenue Recognition</h1><div class=\"kpi-grid\">");
        AppendKpi(sb, "Recognized", d.TotalRecognized.ToString("N0"), "emerald"); AppendKpi(sb, "Scheduled", d.TotalScheduled.ToString("N0"), "sky");
        AppendKpi(sb, "Active Value", d.TotalActiveValue.ToString("N0"), "slate"); AppendKpi(sb, "Active", d.ActiveContractCount.ToString(), "slate");
        sb.Append("</div><table><thead><tr><th>Period</th><th>Recognized</th><th>Scheduled</th></tr></thead><tbody>");
        foreach (var p in d.Periods) sb.AppendFormat("<tr><td>{0}</td><td class=\"text-emerald\">{1:N0}</td><td class=\"text-sky\">{2:N0}</td></tr>", p.PeriodLabel, p.RecognizedAmount, p.ScheduledAmount);
        sb.Append("</tbody></table>");
    }

    private async Task BuildPipelineHtmlAsync(StringBuilder sb, CancellationToken ct)
    {
        var d = await _reportService.GetContractValuePipelineAsync(ct);
        sb.Append("<h1>Value Pipeline</h1><div class=\"kpi-grid\">");
        AppendKpi(sb, "Total Value", d.GrandTotalValue.ToString("N0"), "slate"); AppendKpi(sb, "Count", d.GrandTotalCount.ToString(), "slate"); AppendKpi(sb, "Average", d.AverageContractValue.ToString("N0"), "slate");
        sb.Append("</div><table><thead><tr><th>Status</th><th>Count</th><th>Value</th><th>Share %</th></tr></thead><tbody>");
        foreach (var it in d.Items) sb.AppendFormat("<tr><td>{0}</td><td>{1}</td><td>{2:N0}</td><td>{3}%</td></tr>", it.Status, it.Count, it.TotalValue, it.PercentageOfTotal);
        sb.Append("</tbody></table>");
    }

    private async Task BuildCalendarHtmlAsync(StringBuilder sb, CancellationToken ct)
    {
        var d = await _reportService.GetObligationsCalendarAsync(6, ct);
        sb.Append("<h1>Obligations Calendar</h1><div class=\"kpi-grid\">");
        AppendKpi(sb, "Events", d.TotalEvents.ToString(), "slate"); AppendKpi(sb, "Total Amount", d.TotalAmountDue.ToString("N0"), "rose"); AppendKpi(sb, "Period", d.UpcomingMonths + "m", "slate");
        sb.Append("</div><table><thead><tr><th>Date</th><th>Type</th><th>Count</th><th>Amount</th></tr></thead><tbody>");
        foreach (var e in d.Events) sb.AppendFormat("<tr><td>{0:yyyy-MM-dd}</td><td>{1}</td><td>{2}</td><td>{3:N0}</td></tr>", e.Date, e.ObligationTypeDisplay, e.Count, e.TotalAmount);
        sb.Append("</tbody></table>");
    }

    private async Task BuildExposureHtmlAsync(StringBuilder sb, CancellationToken ct)
    {
        var d = await _reportService.GetFinancialExposureAsync(ct);
        sb.Append("<h1>Financial Exposure</h1><div class=\"kpi-grid\">");
        AppendKpi(sb, "Active Value", d.TotalActiveValue.ToString("N0"), "emerald"); AppendKpi(sb, "At Risk", d.AtRiskValue.ToString("N0"), "rose");
        AppendKpi(sb, "Pending", d.PendingApprovalValue.ToString("N0"), "amber"); AppendKpi(sb, "Exposure Ratio", d.ExposureRatioPercent + "%", "slate");
        sb.Append("</div><div class=\"kpi-grid\">");
        AppendKpi(sb, "Recognized Revenue", d.TotalRecognizedRevenue.ToString("N0"), "sky"); AppendKpi(sb, "Receivables", d.TotalOutstandingReceivables.ToString("N0"), "slate");
        sb.Append("</div>");
        if (d.CurrencyBreakdown.Count > 0)
        {
            sb.Append("<h2>Currency Breakdown</h2><table><thead><tr><th>Currency</th><th>Amount</th><th>Cnt</th><th>Share %</th></tr></thead><tbody>");
            foreach (var c in d.CurrencyBreakdown) sb.AppendFormat("<tr><td>{0}</td><td>{1:N0}</td><td>{2}</td><td>{3}%</td></tr>", c.CurrencyCode, c.TotalAmount, c.ContractCount, c.Percentage);
            sb.Append("</tbody></table>");
        }
    }

    private static void AppendKpi(StringBuilder sb, string label, string value, string color) { sb.Append("<div class=\"kpi-card\"><div class=\"kpi-label\">").Append(label).Append("</div><div class=\"kpi-value text-").Append(color).Append("\">").Append(value).Append("</div></div>"); }

    private static async Task<byte[]> HtmlToPdfAsync(string html, CancellationToken ct)
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true, Args = new[] { "--no-sandbox", "--disable-setuid-sandbox" } });
        var page = await browser.NewPageAsync(new BrowserNewPageOptions { ViewportSize = new ViewportSize { Width = 1200, Height = 800 } });
        await page.SetContentAsync(html);
        return await page.PdfAsync(new PagePdfOptions { Format = PaperFormat.A4, Margin = new Margin { Top = "15mm", Bottom = "15mm", Left = "15mm", Right = "15mm" }, PrintBackground = true });
    }
}