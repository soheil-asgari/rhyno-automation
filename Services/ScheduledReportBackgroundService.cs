using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Modules.Finance.Infrastructure.Persistence;
using OfficeAutomation.Modules.Office.Infrastructure.Persistence;
using OfficeAutomation.Modules.Identity.Infrastructure.Persistence;
using OfficeAutomation.Services.Tenancy;

namespace OfficeAutomation.Services;

/// <summary>
/// BackgroundService برای تولید و ارسال خودکار گزارش‌های دوره‌ای
/// </summary>
public sealed class ScheduledReportBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ITenantRegistry _tenantRegistry;
    private readonly ILogger<ScheduledReportBackgroundService> _logger;

    public ScheduledReportBackgroundService(
        IServiceScopeFactory scopeFactory,
        ITenantRegistry tenantRegistry,
        ILogger<ScheduledReportBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _tenantRegistry = tenantRegistry;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = DateTime.Now;
                var tenants = await _tenantRegistry.GetAllTenantsAsync(stoppingToken);

                foreach (var tenant in tenants)
                {
                    using var scope = _scopeFactory.CreateScope();
                    var tenantExecutionScope = scope.ServiceProvider.GetRequiredService<ITenantExecutionScope>();
                    using var _ = tenantExecutionScope.BeginScope(tenant.TenantId);

                    await GenerateAndSendDailyReportsAsync(scope.ServiceProvider, now, stoppingToken);
                    await GenerateAndSendWeeklyReportsAsync(scope.ServiceProvider, now, stoppingToken);
                    await GenerateAndSendMonthlyReportsAsync(scope.ServiceProvider, now, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while generating scheduled reports.");
            }

            // Check every 30 minutes
            await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken);
        }
    }

    private async Task GenerateAndSendDailyReportsAsync(IServiceProvider services, DateTime now, CancellationToken ct)
    {
        // Daily reports run at 6:00 AM
        if (now.Hour != 6) return;

        var exportService = services.GetRequiredService<IExportService>();
        var emailService = services.GetRequiredService<IEmailService>();
        var identityContext = services.GetRequiredService<IdentityDbContext>();

        // Get admin users who should receive reports
        var adminUsers = await identityContext.Users
            .AsNoTracking()
            .Where(u => u.IsManager)
            .Take(20)
            .ToListAsync(ct);

        foreach (var user in adminUsers)
        {
            if (string.IsNullOrWhiteSpace(user.Email)) continue;

            try
            {
                var data = new System.Data.DataTable("DailySummary");
                data.Columns.Add("عنوان");
                data.Columns.Add("مقدار");
                data.Rows.Add("تاریخ گزارش", now.ToString("yyyy/MM/dd"));

                var reportBytes = exportService.ExportToExcel(data, "گزارش روزانه");
                await emailService.SendScheduledReportEmailAsync(
                    user.Email,
                    user.FullName ?? user.UserName ?? "کاربر",
                    $"گزارش روزانه - {now:yyyy/MM/dd}",
                    reportBytes,
                    $"DailyReport_{now:yyyyMMdd}.xlsx",
                    ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send daily report to {Email}", user.Email);
            }
        }
    }

    private async Task GenerateAndSendWeeklyReportsAsync(IServiceProvider services, DateTime now, CancellationToken ct)
    {
        // Weekly reports run on Saturdays at 7:00 AM
        if (now.DayOfWeek != DayOfWeek.Saturday || now.Hour != 7) return;

        var exportService = services.GetRequiredService<IExportService>();
        var emailService = services.GetRequiredService<IEmailService>();
        var financeContext = services.GetRequiredService<FinanceDbContext>();
        var identityContext = services.GetRequiredService<IdentityDbContext>();

        var adminUsers = await identityContext.Users
            .AsNoTracking()
            .Where(u => u.IsManager)
            .Take(20)
            .ToListAsync(ct);

        var weekStart = now.AddDays(-7).Date;
        var invoices = await financeContext.Invoices
            .AsNoTracking()
            .Where(i => i.CreatedAt >= weekStart && i.CreatedAt <= now)
            .Select(i => new { i.InvoiceNumber, i.InvoiceType, i.PartyName, i.GrandTotal, i.WorkflowStatus })
            .ToListAsync(ct);

        var data = new System.Data.DataTable("WeeklyInvoices");
        data.Columns.Add("شماره فاکتور");
        data.Columns.Add("نوع");
        data.Columns.Add("طرف حساب");
        data.Columns.Add("مبلغ کل");
        data.Columns.Add("وضعیت");

        foreach (var inv in invoices)
        {
            data.Rows.Add(inv.InvoiceNumber, inv.InvoiceType == "Sale" ? "فروش" : "خرید",
                inv.PartyName, inv.GrandTotal, inv.WorkflowStatus);
        }

        var reportBytes = exportService.ExportToExcel(data, "گزارش هفتگی فاکتورها");
        foreach (var user in adminUsers)
        {
            if (string.IsNullOrWhiteSpace(user.Email)) continue;
            try
            {
                await emailService.SendScheduledReportEmailAsync(
                    user.Email,
                    user.FullName ?? user.UserName ?? "کاربر",
                    $"گزارش هفتگی فاکتورها - {now:yyyy/MM/dd}",
                    reportBytes,
                    $"WeeklyInvoices_{now:yyyyMMdd}.xlsx",
                    ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send weekly report to {Email}", user.Email);
            }
        }
    }

    private async Task GenerateAndSendMonthlyReportsAsync(IServiceProvider services, DateTime now, CancellationToken ct)
    {
        // Monthly reports run on the 1st of each month at 8:00 AM
        if (now.Day != 1 || now.Hour != 8) return;

        var exportService = services.GetRequiredService<IExportService>();
        var emailService = services.GetRequiredService<IEmailService>();
        var financeContext = services.GetRequiredService<FinanceDbContext>();
        var officeContext = services.GetRequiredService<OfficeDbContext>();
        var identityContext = services.GetRequiredService<IdentityDbContext>();

        var adminUsers = await identityContext.Users
            .AsNoTracking()
            .Where(u => u.IsManager)
            .Take(20)
            .ToListAsync(ct);

        var monthStart = new DateTime(now.Year, now.Month, 1).AddMonths(-1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);

        var payrollItems = await financeContext.PayrollLists
            .AsNoTracking()
            .Where(pl => pl.Year == monthStart.Year && pl.Month == monthStart.Month)
            .SelectMany(pl => pl.Items)
            .Select(i => new { PayrollListYear = i.PayrollList.Year, PayrollListMonth = i.PayrollList.Month, i.EmployeeName, i.NetPayable })
            .ToListAsync(ct);

        var leaves = await officeContext.Leaves
            .AsNoTracking()
            .Where(l => l.StartDate >= monthStart && l.StartDate <= monthEnd)
            .Select(l => new { l.UserId, l.Status, l.StartDate, l.EndDate })
            .ToListAsync(ct);

        var data = new System.Data.DataTable("MonthlySummary");
        data.Columns.Add("شاخص");
        data.Columns.Add("مقدار");
        data.Rows.Add("دوره", $"{monthStart:yyyy/MM} - {monthEnd:yyyy/MM}");
        data.Rows.Add("تعداد فیش حقوقی", payrollItems.Count);
        data.Rows.Add("مبلغ کل حقوق", payrollItems.Sum(p => p.NetPayable));
        data.Rows.Add("تعداد مرخصی‌ها", leaves.Count);

        var reportBytes = exportService.ExportToExcel(data, "گزارش ماهانه");
        foreach (var user in adminUsers)
        {
            if (string.IsNullOrWhiteSpace(user.Email)) continue;
            try
            {
                await emailService.SendScheduledReportEmailAsync(
                    user.Email,
                    user.FullName ?? user.UserName ?? "کاربر",
                    $"گزارش ماهانه - {monthStart:yyyy/MM}",
                    reportBytes,
                    $"MonthlyReport_{monthStart:yyyyMM}.xlsx",
                    ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send monthly report to {Email}", user.Email);
            }
        }
    }
}