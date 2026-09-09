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
        var ws = wb.Worksheets.Add("قراردادها");
        ws.RightToLeft = true;
        var headers = new[] { "شماره قرارداد", "عنوان", "نوع قرارداد", "وضعیت", "مبلغ کل", "تاریخ شروع", "تاریخ پایان", "مسئول", "تاریخ ایجاد" };
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

    public async Task<byte[]> ExportContractsToPdfAsync(ContractIndexVM filters, string currentUserId, CancellationToken ct = default)
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

        var sb = new StringBuilder();
        sb.Append("<html><head><meta charset=\"utf-8\"><style>");
        sb.Append("body{font-family:Tahoma,Arial,sans-serif;font-size:11px;direction:rtl;text-align:right;padding:20px;}");
        sb.Append("h1{font-size:16px;color:#0f766b;margin-bottom:16px;text-align:right;}");
        sb.Append("p{margin-bottom:12px;color:#64748b;text-align:right;}");
        sb.Append("table{width:100%;border-collapse:collapse;direction:rtl;}");
        sb.Append("th{background:#0f766b;color:white;padding:6px 4px;font-size:10px;text-align:center;}");
        sb.Append("td{padding:5px 4px;border:1px solid #ddd;text-align:center;font-size:10px;}");
        sb.Append("tr:nth-child(even){background:#f9f9f9;}");
        sb.Append("</style></head><body>");
        sb.Append("<h1>فهرست قراردادها</h1>");
        sb.Append("<p>تاریخ گزارش: ").Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm")).Append("</p>");
        sb.Append("<table><thead><tr>");
        sb.Append("<th>شماره قرارداد</th><th>عنوان</th><th>نوع قرارداد</th><th>وضعیت</th><th>مبلغ کل</th><th>تاریخ شروع</th><th>تاریخ پایان</th><th>مسئول</th>");
        sb.Append("</tr></thead><tbody>");
        foreach (var c in contracts)
        {
            sb.Append("<tr>");
            sb.Append("<td>").Append(c.ContractNumber).Append("</td>");
            sb.Append("<td>").Append(c.Title).Append("</td>");
            sb.Append("<td>").Append(c.ContractType).Append("</td>");
            sb.Append("<td>").Append(c.Status).Append("</td>");
            sb.Append("<td>").Append(c.TotalAmountInclusiveTax.ToString("N0")).Append("</td>");
            sb.Append("<td>").Append(c.StartDate.ToString("yyyy-MM-dd")).Append("</td>");
            sb.Append("<td>").Append(c.EndDate?.ToString("yyyy-MM-dd") ?? "").Append("</td>");
            sb.Append("<td>").Append(c.AssignedTo).Append("</td>");
            sb.Append("</tr>");
        }
        sb.Append("</tbody></table></body></html>");

        return await HtmlToPdfAsync(sb.ToString(), ct);
    }

    public async Task<byte[]> ExportReportToExcelAsync(string reportType, CancellationToken ct = default)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add(reportType);
        ws.RightToLeft = true;
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
        ws.Cell(1, 1).Value = "گزارش کهنگی مطالبات و پرداخت‌ها"; ws.Cell(1, 1).Style.Font.Bold = true; ws.Cell(1, 1).Style.Font.FontSize = 14;
        ws.Cell(3, 1).Value = "مجموع معوقات"; ws.Cell(3, 2).Value = d.TotalOverdue; ws.Cell(3, 2).Style.NumberFormat.Format = "#,##0";
        ws.Cell(4, 1).Value = "تعداد"; ws.Cell(4, 2).Value = d.TotalOverdueCount;
        ws.Cell(5, 1).Value = "میانگین مبلغ"; ws.Cell(5, 2).Value = d.AverageOverdueAmount; ws.Cell(5, 2).Style.NumberFormat.Format = "#,##0";
        ws.Cell(6, 1).Value = "حداکثر تاخیر (روز)"; ws.Cell(6, 2).Value = d.MaxDaysOverdue;
        ws.Cell(8, 1).Value = "بازه زمانی"; ws.Cell(8, 2).Value = "تعداد"; ws.Cell(8, 3).Value = "مبلغ کل"; ws.Row(8).Style.Font.Bold = true;
        for (int i = 0; i < d.Buckets.Count; i++) { var b = d.Buckets[i]; ws.Cell(9 + i, 1).Value = b.BucketName; ws.Cell(9 + i, 2).Value = b.Count; ws.Cell(9 + i, 3).Value = b.TotalAmount; ws.Cell(9 + i, 3).Style.NumberFormat.Format = "#,##0"; }
    }

    private async Task WriteRevenueAsync(IXLWorksheet ws, CancellationToken ct)
    {
        var d = await _reportService.GetRevenueRecognitionScheduleAsync(ct);
        ws.Cell(1, 1).Value = "شناسایی و تحقق درآمد"; ws.Cell(1, 1).Style.Font.Bold = true; ws.Cell(1, 1).Style.Font.FontSize = 14;
        ws.Cell(3, 1).Value = "تحقق‌یافته"; ws.Cell(3, 2).Value = d.TotalRecognized; ws.Cell(3, 2).Style.NumberFormat.Format = "#,##0";
        ws.Cell(4, 1).Value = "زمان‌بندی‌شده"; ws.Cell(4, 2).Value = d.TotalScheduled; ws.Cell(4, 2).Style.NumberFormat.Format = "#,##0";
        ws.Cell(5, 1).Value = "ارزش قراردادهای فعال"; ws.Cell(5, 2).Value = d.TotalActiveValue; ws.Cell(5, 2).Style.NumberFormat.Format = "#,##0";
        ws.Cell(6, 1).Value = "تعداد فعال"; ws.Cell(6, 2).Value = d.ActiveContractCount;
        ws.Cell(8, 1).Value = "دوره"; ws.Cell(8, 2).Value = "تحقق‌یافته"; ws.Cell(8, 3).Value = "زمان‌بندی‌شده"; ws.Row(8).Style.Font.Bold = true;
        for (int i = 0; i < d.Periods.Count; i++) { var p = d.Periods[i]; ws.Cell(9 + i, 1).Value = p.PeriodLabel; ws.Cell(9 + i, 2).Value = p.RecognizedAmount; ws.Cell(9 + i, 2).Style.NumberFormat.Format = "#,##0"; ws.Cell(9 + i, 3).Value = p.ScheduledAmount; ws.Cell(9 + i, 3).Style.NumberFormat.Format = "#,##0"; }
    }

    private async Task WritePipelineAsync(IXLWorksheet ws, CancellationToken ct)
    {
        var d = await _reportService.GetContractValuePipelineAsync(ct);
        ws.Cell(1, 1).Value = "پایپ‌لاین ارزش قراردادها"; ws.Cell(1, 1).Style.Font.Bold = true; ws.Cell(1, 1).Style.Font.FontSize = 14;
        ws.Cell(3, 1).Value = "مجموع کل"; ws.Cell(3, 2).Value = d.GrandTotalValue; ws.Cell(3, 2).Style.NumberFormat.Format = "#,##0";
        ws.Cell(4, 1).Value = "تعداد"; ws.Cell(4, 2).Value = d.GrandTotalCount;
        ws.Cell(5, 1).Value = "میانگین"; ws.Cell(5, 2).Value = d.AverageContractValue; ws.Cell(5, 2).Style.NumberFormat.Format = "#,##0";
        ws.Cell(7, 1).Value = "وضعیت"; ws.Cell(7, 2).Value = "تعداد"; ws.Cell(7, 3).Value = "ارزش"; ws.Cell(7, 4).Value = "سهم (%)"; ws.Row(7).Style.Font.Bold = true;
        for (int i = 0; i < d.Items.Count; i++) { var it = d.Items[i]; ws.Cell(8 + i, 1).Value = it.Status; ws.Cell(8 + i, 2).Value = it.Count; ws.Cell(8 + i, 3).Value = it.TotalValue; ws.Cell(8 + i, 3).Style.NumberFormat.Format = "#,##0"; ws.Cell(8 + i, 4).Value = it.PercentageOfTotal; }
    }
private async Task WriteCalendarAsync(IXLWorksheet ws, CancellationToken ct)
    {
        var d = await _reportService.GetObligationsCalendarAsync(6, ct);
        ws.Cell(1, 1).Value = "تقویم تعهدات و سررسیدها"; ws.Cell(1, 1).Style.Font.Bold = true; ws.Cell(1, 1).Style.Font.FontSize = 14;
        ws.Cell(3, 1).Value = "مجموع رویدادها"; ws.Cell(3, 2).Value = d.TotalEvents;
        ws.Cell(4, 1).Value = "مبلغ کل سررسید"; ws.Cell(4, 2).Value = d.TotalAmountDue; ws.Cell(4, 2).Style.NumberFormat.Format = "#,##0";
        ws.Cell(5, 1).Value = "دوره"; ws.Cell(5, 2).Value = d.UpcomingMonths + " ماه آینده";
        ws.Cell(7, 1).Value = "تاریخ"; ws.Cell(7, 2).Value = "نوع تعهد"; ws.Cell(7, 3).Value = "تعداد"; ws.Cell(7, 4).Value = "مبلغ"; ws.Row(7).Style.Font.Bold = true;
        for (int i = 0; i < d.Events.Count; i++) { var e = d.Events[i]; ws.Cell(8 + i, 1).Value = e.Date.ToString("yyyy-MM-dd"); ws.Cell(8 + i, 2).Value = e.ObligationTypeDisplay; ws.Cell(8 + i, 3).Value = e.Count; ws.Cell(8 + i, 4).Value = e.TotalAmount; ws.Cell(8 + i, 4).Style.NumberFormat.Format = "#,##0"; }
    }

    private async Task WriteExposureAsync(IXLWorksheet ws, CancellationToken ct)
    {
        var d = await _reportService.GetFinancialExposureAsync(ct);
        ws.Cell(1, 1).Value = "تحلیل ریسک و مواجهه مالی"; ws.Cell(1, 1).Style.Font.Bold = true; ws.Cell(1, 1).Style.Font.FontSize = 14;
        ws.Cell(3, 1).Value = "ارزش قراردادهای فعال"; ws.Cell(3, 2).Value = d.TotalActiveValue; ws.Cell(3, 2).Style.NumberFormat.Format = "#,##0";
        ws.Cell(4, 1).Value = "تعداد فعال"; ws.Cell(4, 2).Value = d.ActiveContractCount;
        ws.Cell(5, 1).Value = "در معرض ریسک"; ws.Cell(5, 2).Value = d.AtRiskValue; ws.Cell(5, 2).Style.NumberFormat.Format = "#,##0";
        ws.Cell(6, 1).Value = "تعداد ریسک"; ws.Cell(6, 2).Value = d.AtRiskCount;
        ws.Cell(7, 1).Value = "در انتظار تأیید"; ws.Cell(7, 2).Value = d.PendingApprovalValue; ws.Cell(7, 2).Style.NumberFormat.Format = "#,##0";
        ws.Cell(8, 1).Value = "تعداد در انتظار"; ws.Cell(8, 2).Value = d.PendingApprovalCount;
        ws.Cell(9, 1).Value = "مطالبات معوق"; ws.Cell(9, 2).Value = d.TotalOutstandingReceivables; ws.Cell(9, 2).Style.NumberFormat.Format = "#,##0";
        ws.Cell(10, 1).Value = "درآمد شناسایی‌شده"; ws.Cell(10, 2).Value = d.TotalRecognizedRevenue; ws.Cell(10, 2).Style.NumberFormat.Format = "#,##0";
        ws.Cell(11, 1).Value = "نسبت مواجهه ریسک"; ws.Cell(11, 2).Value = d.ExposureRatioPercent + "%";
        if (d.CurrencyBreakdown.Count > 0)
        {
            ws.Cell(13, 1).Value = "ارز"; ws.Cell(13, 2).Value = "مبلغ"; ws.Cell(13, 3).Value = "تعداد"; ws.Cell(13, 4).Value = "سهم (%)"; ws.Row(13).Style.Font.Bold = true;
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
        sb.Append("<h1>گزارش کهنگی مطالبات و پرداخت‌ها</h1><div class=\"kpi-grid\">");
        AppendKpi(sb, "مجموع معوقات", d.TotalOverdue.ToString("N0"), "rose"); AppendKpi(sb, "تعداد", d.TotalOverdueCount.ToString(), "slate");
        AppendKpi(sb, "میانگین مبلغ", d.AverageOverdueAmount.ToString("N0"), "slate"); AppendKpi(sb, "حداکثر تاخیر", d.MaxDaysOverdue + " روز", "amber");
        sb.Append("</div><table><thead><tr><th>بازه زمانی</th><th>تعداد</th><th>مبلغ کل</th></tr></thead><tbody>");
        foreach (var b in d.Buckets) sb.AppendFormat("<tr><td>{0}</td><td>{1}</td><td>{2:N0}</td></tr>", b.BucketName, b.Count, b.TotalAmount);
        sb.Append("</tbody></table>");
    }

    private async Task BuildRevenueHtmlAsync(StringBuilder sb, CancellationToken ct)
    {
        var d = await _reportService.GetRevenueRecognitionScheduleAsync(ct);
        sb.Append("<h1>شناسایی و تحقق درآمد</h1><div class=\"kpi-grid\">");
        AppendKpi(sb, "تحقق‌یافته", d.TotalRecognized.ToString("N0"), "emerald"); AppendKpi(sb, "زمان‌بندی‌شده", d.TotalScheduled.ToString("N0"), "sky");
        AppendKpi(sb, "ارزش قراردادهای فعال", d.TotalActiveValue.ToString("N0"), "slate"); AppendKpi(sb, "تعداد فعال", d.ActiveContractCount.ToString(), "slate");
        sb.Append("</div><table><thead><tr><th>دوره</th><th>تحقق‌یافته</th><th>زمان‌بندی‌شده</th></tr></thead><tbody>");
        foreach (var p in d.Periods) sb.AppendFormat("<tr><td>{0}</td><td class=\"text-emerald\">{1:N0}</td><td class=\"text-sky\">{2:N0}</td></tr>", p.PeriodLabel, p.RecognizedAmount, p.ScheduledAmount);
        sb.Append("</tbody></table>");
    }

    private async Task BuildPipelineHtmlAsync(StringBuilder sb, CancellationToken ct)
    {
        var d = await _reportService.GetContractValuePipelineAsync(ct);
        sb.Append("<h1>پایپ‌لاین ارزش قراردادها</h1><div class=\"kpi-grid\">");
        AppendKpi(sb, "مجموع کل", d.GrandTotalValue.ToString("N0"), "slate"); AppendKpi(sb, "تعداد", d.GrandTotalCount.ToString(), "slate"); AppendKpi(sb, "میانگین", d.AverageContractValue.ToString("N0"), "slate");
        sb.Append("</div><table><thead><tr><th>وضعیت</th><th>تعداد</th><th>ارزش</th><th>سهم (%)</th></tr></thead><tbody>");
        foreach (var it in d.Items) sb.AppendFormat("<tr><td>{0}</td><td>{1}</td><td>{2:N0}</td><td>{3}%</td></tr>", it.Status, it.Count, it.TotalValue, it.PercentageOfTotal);
        sb.Append("</tbody></table>");
    }

    private async Task BuildCalendarHtmlAsync(StringBuilder sb, CancellationToken ct)
    {
        var d = await _reportService.GetObligationsCalendarAsync(6, ct);
        sb.Append("<h1>تقویم تعهدات و سررسیدها</h1><div class=\"kpi-grid\">");
        AppendKpi(sb, "مجموع رویدادها", d.TotalEvents.ToString(), "slate"); AppendKpi(sb, "مبلغ کل سررسید", d.TotalAmountDue.ToString("N0"), "rose"); AppendKpi(sb, "دوره", d.UpcomingMonths + " ماه آینده", "slate");
        sb.Append("</div><table><thead><tr><th>تاریخ</th><th>نوع تعهد</th><th>تعداد</th><th>مبلغ</th></tr></thead><tbody>");
        foreach (var e in d.Events) sb.AppendFormat("<tr><td>{0:yyyy-MM-dd}</td><td>{1}</td><td>{2}</td><td>{3:N0}</td></tr>", e.Date, e.ObligationTypeDisplay, e.Count, e.TotalAmount);
        sb.Append("</tbody></table>");
    }

    private async Task BuildExposureHtmlAsync(StringBuilder sb, CancellationToken ct)
    {
        var d = await _reportService.GetFinancialExposureAsync(ct);
        sb.Append("<h1>تحلیل ریسک و مواجهه مالی</h1><div class=\"kpi-grid\">");
        AppendKpi(sb, "ارزش قراردادهای فعال", d.TotalActiveValue.ToString("N0"), "emerald"); AppendKpi(sb, "در معرض ریسک", d.AtRiskValue.ToString("N0"), "rose");
        AppendKpi(sb, "در انتظار تأیید", d.PendingApprovalValue.ToString("N0"), "amber"); AppendKpi(sb, "نسبت مواجهه ریسک", d.ExposureRatioPercent + "%", "slate");
        sb.Append("</div><div class=\"kpi-grid\">");
        AppendKpi(sb, "درآمد شناسایی‌شده", d.TotalRecognizedRevenue.ToString("N0"), "sky"); AppendKpi(sb, "مطالبات معوق", d.TotalOutstandingReceivables.ToString("N0"), "slate");
        sb.Append("</div>");
        if (d.CurrencyBreakdown.Count > 0)
        {
            sb.Append("<h2>تفکیک ارزها</h2><table><thead><tr><th>ارز</th><th>مبلغ</th><th>تعداد</th><th>سهم (%)</th></tr></thead><tbody>");
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