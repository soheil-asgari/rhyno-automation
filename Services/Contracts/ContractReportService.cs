using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Models;
using OfficeAutomation.Modules.Office.Infrastructure.Persistence;

namespace OfficeAutomation.Services.Contracts;

public sealed class ContractReportService : IContractReportService
{
    private readonly OfficeDbContext _context;
    private readonly ILogger<ContractReportService> _logger;

    public ContractReportService(OfficeDbContext context, ILogger<ContractReportService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<PaymentAgingReportVM> GetPaymentAgingReportAsync(CancellationToken ct = default)
    {
        var today = DateTime.Today;

        var overduePayments = await _context.ContractPayments
            .AsNoTracking()
            .Include(p => p.Contract)
            .Where(p => p.DueDate < today && p.Status != "Paid" && p.Status != "Waived")
            .Select(p => new
            {
                ContractId = p.ContractId,
                ContractNumber = p.Contract!.ContractNumber,
                Title = p.Contract.Title,
                ClientName = p.Contract.ClientName,
                Amount = p.Amount,
                DueDate = p.DueDate!.Value,
                DaysOverdue = EF.Functions.DateDiffDay(p.DueDate!.Value, today)
            })
            .ToListAsync(ct);

        var buckets = new List<AgingBucketVM>
        {
            new() { BucketName = "0-30 روز" },
            new() { BucketName = "31-60 روز" },
            new() { BucketName = "61-90 روز" },
            new() { BucketName = "بیش از 90 روز" }
        };

        foreach (var p in overduePayments)
        {
            var bucket = p.DaysOverdue <= 30 ? buckets[0]
                       : p.DaysOverdue <= 60 ? buckets[1]
                       : p.DaysOverdue <= 90 ? buckets[2]
                       : buckets[3];

            bucket.Count++;
            bucket.TotalAmount += p.Amount;
            bucket.Contracts.Add(new AgingContractItemVM
            {
                ContractId = p.ContractId,
                ContractNumber = p.ContractNumber,
                Title = p.Title,
                ClientName = p.ClientName,
                Amount = p.Amount,
                DaysOverdue = p.DaysOverdue,
                DueDate = p.DueDate
            });
        }

        return new PaymentAgingReportVM
        {
            Buckets = buckets,
            TotalOverdue = overduePayments.Sum(p => p.Amount),
            TotalOverdueCount = overduePayments.Count,
            AverageOverdueAmount = overduePayments.Count > 0 ? overduePayments.Average(p => p.Amount) : 0,
            MaxDaysOverdue = overduePayments.Count > 0 ? overduePayments.Max(p => p.DaysOverdue) : 0
        };
    }

    public async Task<RevenueRecognitionReportVM> GetRevenueRecognitionScheduleAsync(CancellationToken ct = default)
    {
        var activeContracts = await _context.Contracts
            .AsNoTracking()
            .Where(c => c.Status == ContractStatuses.Active || c.Status == ContractStatuses.ExpiringSoon || c.Status == ContractStatuses.Completed)
            .Select(c => new { c.Id, c.TotalAmount, c.RecognizedRevenue, c.StartDate, c.EndDate, c.Status })
            .ToListAsync(ct);

        var today = DateTime.Today;
        var periods = new List<RevenuePeriodVM>();
        var cumulativeRecognized = 0m;
        var cumulativeChart = new List<ChartDataPoint>();

        for (int i = -6; i <= 12; i++)
        {
            var periodStart = new DateTime(today.Year, today.Month, 1).AddMonths(i);
            if (periodStart > today.AddMonths(12)) break;
            var periodEnd = periodStart.AddMonths(1).AddDays(-1);
            var periodLabel = $"{periodStart.Year:0000}/{periodStart.Month:00}";
            decimal recognizedInPeriod = 0, scheduledInPeriod = 0;

            foreach (var c in activeContracts.Where(c => c.Status == ContractStatuses.Active || c.Status == ContractStatuses.ExpiringSoon))
            {
                if (c.EndDate == null) continue;
                var contractMonths = Math.Max(1, (int)(c.EndDate.Value - c.StartDate).TotalDays / 30);
                var monthlyValue = c.TotalAmount / contractMonths;
                var totalMonthsActive = Math.Max(1, (int)(today - c.StartDate).TotalDays / 30);
                if (totalMonthsActive > 0) recognizedInPeriod += c.RecognizedRevenue / totalMonthsActive;
                if (periodStart > today && periodStart <= c.EndDate.Value) scheduledInPeriod += monthlyValue;
            }
            cumulativeRecognized += recognizedInPeriod;
            periods.Add(new RevenuePeriodVM { PeriodLabel = periodLabel, PeriodStart = periodStart, PeriodEnd = periodEnd, RecognizedAmount = Math.Round(recognizedInPeriod, 0), ScheduledAmount = Math.Round(scheduledInPeriod, 0), TotalContractValueAtPeriodEnd = activeContracts.Sum(c => c.TotalAmount) });
            cumulativeChart.Add(new ChartDataPoint { Label = periodLabel, Value = Math.Round(cumulativeRecognized, 0) });
        }

        var asum = activeContracts.Where(c => c.Status == ContractStatuses.Active || c.Status == ContractStatuses.ExpiringSoon);
        return new RevenueRecognitionReportVM
        {
            Periods = periods, TotalRecognized = activeContracts.Sum(c => c.RecognizedRevenue),
            TotalScheduled = asum.Sum(c => c.TotalAmount - c.RecognizedRevenue),
            TotalActiveValue = asum.Sum(c => c.TotalAmount), ActiveContractCount = asum.Count(),
            CumulativeChart = cumulativeChart
        };
    }

    public async Task<ContractValuePipelineVM> GetContractValuePipelineAsync(CancellationToken ct = default)
    {
        var grouped = await _context.Contracts.AsNoTracking().Where(c => !c.IsArchived)
            .GroupBy(c => c.Status).Select(g => new { Status = g.Key, Count = g.Count(), TotalValue = g.Sum(c => c.TotalAmount) }).ToListAsync(ct);
        var all = await _context.Contracts.AsNoTracking().Where(c => !c.IsArchived).ToListAsync(ct);
        var grandTotalValue = all.Sum(c => c.TotalAmount);
        var grandTotalCount = all.Count;

        var items = grouped.Select(g => new ValuePipelineItemVM
        {
            Status = g.Status, Count = g.Count, TotalValue = g.TotalValue,
            PercentageOfTotal = grandTotalValue > 0 ? Math.Round(g.TotalValue / grandTotalValue * 100, 1) : 0
        }).OrderByDescending(i => i.TotalValue).ToList();

        return new ContractValuePipelineVM
        {
            Items = items, GrandTotalValue = grandTotalValue, GrandTotalCount = grandTotalCount,
            AverageContractValue = grandTotalCount > 0 ? grandTotalValue / grandTotalCount : 0,
            Distribution = items.Select(i => new ChartDataPoint { Label = i.Status, Value = i.TotalValue }).ToList()
        };
    }

    public async Task<ObligationsCalendarVM> GetObligationsCalendarAsync(int months = 6, CancellationToken ct = default)
    {
        var today = DateTime.Today;
        var endDate = today.AddMonths(months);
        var events = new List<ObligationEventVM>();

        var installments = await _context.Set<ContractPaymentScheduleInstallment>()
            .AsNoTracking().Include(i => i.Schedule).ThenInclude(s => s!.Contract)
            .Where(i => i.DueDate >= today && i.DueDate <= endDate && i.Status != "Paid" && i.Status != "Overdue")
            .OrderBy(i => i.DueDate).ToListAsync(ct);

        foreach (var inst in installments)
        {
            var dk = inst.DueDate.Date;
            var e = events.FirstOrDefault(x => x.Date == dk && x.ObligationType == "PaymentDue");
            if (e == null) { e = new ObligationEventVM { Date = dk, ObligationType = "PaymentDue" }; events.Add(e); }
            e.Count++; e.TotalAmount += inst.Amount;
            e.Contracts.Add(new ObligationContractItemVM { ContractId = inst.Schedule?.Contract?.Id ?? 0, ContractNumber = inst.Schedule?.Contract?.ContractNumber ?? "", Title = inst.Schedule?.Contract?.Title ?? "", ClientName = inst.Schedule?.Contract?.ClientName ?? "", Amount = inst.Amount, EventDate = inst.DueDate, Description = $"قسط {inst.InstallmentNumber}" });
        }

        var expiring = await _context.Contracts.AsNoTracking()
            .Where(c => c.EndDate != null && c.EndDate.Value >= today && c.EndDate.Value <= endDate
                && (c.Status == ContractStatuses.Active || c.Status == ContractStatuses.ExpiringSoon))
            .ToListAsync(ct);

        foreach (var c in expiring)
        {
            var dk = c.EndDate!.Value.Date;
            var e = events.FirstOrDefault(x => x.Date == dk && x.ObligationType == "RenewalApproaching");
            if (e == null) { e = new ObligationEventVM { Date = dk, ObligationType = "RenewalApproaching" }; events.Add(e); }
            e.Count++;
            e.Contracts.Add(new ObligationContractItemVM { ContractId = c.Id, ContractNumber = c.ContractNumber, Title = c.Title, ClientName = c.ClientName, Amount = c.TotalAmount, EventDate = c.EndDate!.Value });
        }

        var guarantees = await _context.Set<ContractGuarantee>()
            .AsNoTracking().Include(g => g.Contract)
            .Where(g => g.ExpiryDate >= today && g.ExpiryDate <= endDate && g.Status == "Active")
            .OrderBy(g => g.ExpiryDate).ToListAsync(ct);

        foreach (var g in guarantees)
        {
            var dk = g.ExpiryDate.Date;
            var e = events.FirstOrDefault(x => x.Date == dk && x.ObligationType == "GuaranteeExpiring");
            if (e == null) { e = new ObligationEventVM { Date = dk, ObligationType = "GuaranteeExpiring" }; events.Add(e); }
            e.Count++; e.TotalAmount += g.Amount;
            e.Contracts.Add(new ObligationContractItemVM { ContractId = g.Contract?.Id ?? 0, ContractNumber = g.Contract?.ContractNumber ?? "", Title = g.Contract?.Title ?? "", ClientName = g.Contract?.ClientName ?? "", Amount = g.Amount, EventDate = g.ExpiryDate, Description = g.GuaranteeNumber });
        }

        events = events.OrderBy(e => e.Date).ThenBy(e => e.ObligationType).ToList();

        return new ObligationsCalendarVM
        {
            Events = events, TotalEvents = events.Sum(e => e.Count), TotalAmountDue = events.Sum(e => e.TotalAmount),
            UpcomingMonths = months,
            CountByType = events.GroupBy(e => e.ObligationType).ToDictionary(g => g.Key, g => g.Sum(e => e.Count)),
            AmountByType = events.GroupBy(e => e.ObligationType).ToDictionary(g => g.Key, g => g.Sum(e => e.TotalAmount))
        };
    }

    public async Task<FinancialExposureVM> GetFinancialExposureAsync(CancellationToken ct = default)
    {
        var today = DateTime.Today;
        var active = await _context.Contracts.AsNoTracking()
            .Where(c => c.Status == ContractStatuses.Active || c.Status == ContractStatuses.ExpiringSoon).ToListAsync(ct);
        var pending = await _context.Contracts.AsNoTracking()
            .Where(c => c.Status == ContractStatuses.PendingApproval).ToListAsync(ct);
        var atRisk = await _context.ContractPayments.AsNoTracking()
            .Where(p => p.DueDate < today && p.Status != "Paid" && p.Status != "Waived").ToListAsync(ct);

        return new FinancialExposureVM
        {
            TotalActiveValue = active.Sum(c => c.TotalAmount), ActiveContractCount = active.Count,
            AtRiskValue = atRisk.Sum(p => p.Amount), AtRiskCount = atRisk.Select(p => p.ContractId).Distinct().Count(),
            PendingApprovalValue = pending.Sum(c => c.TotalAmount), PendingApprovalCount = pending.Count,
            TotalOutstandingReceivables = active.Sum(c => c.RemainingBalance),
            TotalRecognizedRevenue = active.Sum(c => c.RecognizedRevenue),
            CurrencyBreakdown = active.GroupBy(c => c.CurrencyCode).Select(g => new CurrencyBreakdownVM
            {
                CurrencyCode = g.Key, TotalAmount = g.Sum(c => c.TotalAmount), ContractCount = g.Count(),
                Percentage = active.Count > 0 ? Math.Round((decimal)g.Count() / active.Count * 100, 1) : 0
            }).ToList()
        };
    }

    public async Task<ReportsIndexVM> GetAllReportsAsync(int obligationsMonths = 6, CancellationToken ct = default)
    {
        return new ReportsIndexVM
        {
            PaymentAging = await GetPaymentAgingReportAsync(ct),
            RevenueRecognition = await GetRevenueRecognitionScheduleAsync(ct),
            ValuePipeline = await GetContractValuePipelineAsync(ct),
            ObligationsCalendar = await GetObligationsCalendarAsync(obligationsMonths, ct),
            FinancialExposure = await GetFinancialExposureAsync(ct),
            ObligationsMonths = obligationsMonths
        };
    }
}
