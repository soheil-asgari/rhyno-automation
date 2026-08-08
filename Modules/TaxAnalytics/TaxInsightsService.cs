using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OfficeAutomation.Modules.Persistence;
using OfficeAutomation.Modules.Persistence.Entities;

namespace OfficeAutomation.Modules.TaxAnalytics;

/// <inheritdoc />
public sealed class TaxInsightsService : ITaxInsightsService
{
    private readonly IDbContextFactory<TaxDbContext> _dbFactory;
    private readonly ILogger<TaxInsightsService> _logger;

    public TaxInsightsService(
        IDbContextFactory<TaxDbContext> dbFactory,
        ILogger<TaxInsightsService> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    public async Task<TaxAnalyticsSnapshot> BuildSnapshotAsync(CancellationToken cancellationToken)
    {
        using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var now = DateTime.Now;
        var monthStart = new DateTime(now.Year, now.Month, 1);
        var prevMonthStart = monthStart.AddMonths(-1);

        var snapshot = new TaxAnalyticsSnapshot
        {
            TotalInvoices = await db.InvoiceRecords.CountAsync(cancellationToken),
            MissingEconomicCodeInvoices = await CountMissingEconomicCodeAsync(db, cancellationToken),
            TotalTaxPaid = await db.InvoiceRecords.SumAsync(r => r.VatAmount, cancellationToken),
            CurrentMonthTaxPaid = await SumTaxSinceAsync(db, monthStart, cancellationToken),
            PreviousMonthTaxPaid = await SumTaxBetweenAsync(db, prevMonthStart, monthStart, cancellationToken),
            PendingSubmissions = await db.MowadiyanSubmissions
                .CountAsync(s => s.Status == MowadiyanSubmissionStatus.PendingSubmission
                                 || s.Status == MowadiyanSubmissionStatus.Draft, cancellationToken),
            RejectedSubmissions = await db.MowadiyanSubmissions
                .CountAsync(s => s.Status == MowadiyanSubmissionStatus.RejectedByTaxOrg
                                 || s.Status == MowadiyanSubmissionStatus.RetryLimitExceeded, cancellationToken)
        };

        if (snapshot.PreviousMonthTaxPaid > 0)
        {
            snapshot.TaxMoMChangePercent =
                ((snapshot.CurrentMonthTaxPaid - snapshot.PreviousMonthTaxPaid) / snapshot.PreviousMonthTaxPaid) * 100m;
        }

        snapshot.Alerts = await BuildAlertsAsync(db, snapshot, cancellationToken);
        _logger.LogInformation("Built tax analytics snapshot with {Count} alerts.", snapshot.Alerts.Count);
        return snapshot;
    }

    public async Task<IReadOnlyList<TaxInsightAlert>> GenerateAlertsAsync(CancellationToken cancellationToken)
    {
        using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var now = DateTime.Now;
        var monthStart = new DateTime(now.Year, now.Month, 1);
        var prevMonthStart = monthStart.AddMonths(-1);

        var snapshot = new TaxAnalyticsSnapshot
        {
            TotalInvoices = await db.InvoiceRecords.CountAsync(cancellationToken),
            MissingEconomicCodeInvoices = await CountMissingEconomicCodeAsync(db, cancellationToken),
            TotalTaxPaid = await db.InvoiceRecords.SumAsync(r => r.VatAmount, cancellationToken),
            CurrentMonthTaxPaid = await SumTaxSinceAsync(db, monthStart, cancellationToken),
            PreviousMonthTaxPaid = await SumTaxBetweenAsync(db, prevMonthStart, monthStart, cancellationToken),
            PendingSubmissions = await db.MowadiyanSubmissions.CountAsync(
                s => s.Status == MowadiyanSubmissionStatus.PendingSubmission
                     || s.Status == MowadiyanSubmissionStatus.Draft, cancellationToken),
            RejectedSubmissions = await db.MowadiyanSubmissions.CountAsync(
                s => s.Status == MowadiyanSubmissionStatus.RejectedByTaxOrg
                     || s.Status == MowadiyanSubmissionStatus.RetryLimitExceeded, cancellationToken)
        };

        if (snapshot.PreviousMonthTaxPaid > 0)
        {
            snapshot.TaxMoMChangePercent =
                ((snapshot.CurrentMonthTaxPaid - snapshot.PreviousMonthTaxPaid) / snapshot.PreviousMonthTaxPaid) * 100m;
        }

        return await BuildAlertsAsync(db, snapshot, cancellationToken);
    }
// ──────────────────────────────────────────────────────────────────────────
    // Alert generation
    // ──────────────────────────────────────────────────────────────────────────

    private async Task<List<TaxInsightAlert>> BuildAlertsAsync(
        TaxDbContext db,
        TaxAnalyticsSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        var alerts = new List<TaxInsightAlert>();

        // Month-over-month tax paid change.
        if (snapshot.TaxMoMChangePercent.HasValue)
        {
            var change = snapshot.TaxMoMChangePercent.Value;
            if (change >= 30)
            {
                alerts.Add(new TaxInsightAlert
                {
                    Code = "TAX_MOM_INCREASE",
                    Severity = InsightSeverity.Warning,
                    Title = "افزایش مالیات پرداختی",
                    Message = $"مالیات پرداختی این ماه {change:N1}٪ نسبت به ماه گذشته افزایش یافته است.",
                    MetricValue = change
                });
            }
            else if (change <= -30)
            {
                alerts.Add(new TaxInsightAlert
                {
                    Code = "TAX_MOM_DECREASE",
                    Severity = InsightSeverity.Info,
                    Title = "کاهش مالیات پرداختی",
                    Message = $"مالیات پرداختی این ماه {Math.Abs(change):N1}٪ نسبت به ماه گذشته کاهش یافته است.",
                    MetricValue = change
                });
            }
        }

        // Invoices missing an economic code.
        if (snapshot.MissingEconomicCodeInvoices > 0)
        {
            alerts.Add(new TaxInsightAlert
            {
                Code = "MISSING_ECONOMIC_CODE",
                Severity = snapshot.MissingEconomicCodeInvoices >= 5 ? InsightSeverity.Critical : InsightSeverity.Warning,
                Title = "فاکتورهای فاقد کد اقتصادی",
                Message = $"تعداد {snapshot.MissingEconomicCodeInvoices} فاکتور فاقد کد اقتصادی هستند.",
                MetricValue = snapshot.MissingEconomicCodeInvoices
            });
        }

        // Pending submissions to the tax portal.
        if (snapshot.PendingSubmissions > 0)
        {
            alerts.Add(new TaxInsightAlert
            {
                Code = "PENDING_SUBMISSIONS",
                Severity = InsightSeverity.Warning,
                Title = "ارسال در انتظار سامانه مودیان",
                Message = $"{snapshot.PendingSubmissions} فاکتور در صف ارسال به سامانه مودیان قرار دارد.",
                MetricValue = snapshot.PendingSubmissions
            });
        }

        // Rejected submissions.
        if (snapshot.RejectedSubmissions > 0)
        {
            alerts.Add(new TaxInsightAlert
            {
                Code = "REJECTED_SUBMISSIONS",
                Severity = InsightSeverity.Critical,
                Title = "ارسال‌های رد شده",
                Message = $"{snapshot.RejectedSubmissions} ارسال توسط سامانه مودیان رد شده است.",
                MetricValue = snapshot.RejectedSubmissions
            });
        }

        return alerts;
    }

    private static Task<int> CountMissingEconomicCodeAsync(TaxDbContext db, CancellationToken cancellationToken)
    {
        // An invoice is considered missing an economic code when it has no seller OR its
        // seller has no economic code.
        return db.InvoiceRecords.CountAsync(
            r => r.Seller == null || r.Seller.EconomicCode == null || r.Seller.EconomicCode == string.Empty,
            cancellationToken);
    }

    private static Task<decimal> SumTaxSinceAsync(
        TaxDbContext db, DateTime from, CancellationToken cancellationToken)
        => db.InvoiceRecords.Where(r => r.InvoiceDate >= from).SumAsync(r => r.VatAmount, cancellationToken);

    private static Task<decimal> SumTaxBetweenAsync(
        TaxDbContext db, DateTime from, DateTime to, CancellationToken cancellationToken)
        => db.InvoiceRecords
            .Where(r => r.InvoiceDate >= from && r.InvoiceDate < to)
            .SumAsync(r => r.VatAmount, cancellationToken);
}