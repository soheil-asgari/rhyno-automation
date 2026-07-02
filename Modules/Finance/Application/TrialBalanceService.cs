using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Modules.Finance.Domain;
using OfficeAutomation.Modules.Finance.Infrastructure.Persistence;

namespace OfficeAutomation.Modules.Finance.Application;

public sealed class TrialBalanceService
{
    private readonly FinanceDbContext _context;

    public TrialBalanceService(FinanceDbContext context)
    {
        _context = context;
    }

    public Task<TrialBalanceDto> GetTrialBalanceAsync(
        Guid fiscalPeriodId,
        bool includeMoatagh,
        bool groupByFloatingDetail,
        CancellationToken cancellationToken = default)
    {
        return GetTrialBalanceAsync(
            new TrialBalanceRequest(fiscalPeriodId, includeMoatagh, groupByFloatingDetail),
            cancellationToken);
    }

    public async Task<TrialBalanceDto> GetTrialBalanceAsync(
        TrialBalanceRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.FiscalPeriodId == Guid.Empty)
        {
            throw new ArgumentException("Fiscal period id is required.", nameof(request));
        }

        var period = await _context.FiscalPeriods
            .AsNoTracking()
            .Where(item => item.Id == request.FiscalPeriodId)
            .Select(item => new
            {
                item.Id,
                item.Name,
                StartDate = item.StartDate.Date,
                EndDate = item.EndDate.Date
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException($"Fiscal period '{request.FiscalPeriodId}' was not found.");

        var from = period.StartDate;
        var fromExclusive = from.AddDays(1);
        var toExclusive = period.EndDate.AddDays(1);
        var openingJournalTypeIds = await _context.JournalTypes
            .AsNoTracking()
            .Where(item => item.Code == JournalTypeCodes.Opening)
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);

        var scopedLines = _context.VoucherLines
            .AsNoTracking()
            .Where(line =>
                line.VoucherHeader.VoucherDate < toExclusive &&
                line.VoucherHeader.TotalDebits == line.VoucherHeader.TotalCredits &&
                (request.IncludeMoatagh || line.VoucherHeader.Status == VoucherStatus.Permanent));

        var openingLines = scopedLines
            .Where(line =>
                line.VoucherHeader.VoucherDate < from ||
                (openingJournalTypeIds.Contains(line.VoucherHeader.JournalTypeId) &&
                 line.VoucherHeader.VoucherDate >= from &&
                 line.VoucherHeader.VoucherDate < fromExclusive));

        var periodLines = scopedLines
            .Where(line =>
                line.VoucherHeader.VoucherDate >= from &&
                line.VoucherHeader.VoucherDate < toExclusive &&
                !(openingJournalTypeIds.Contains(line.VoucherHeader.JournalTypeId) &&
                  line.VoucherHeader.VoucherDate >= from &&
                  line.VoucherHeader.VoucherDate < fromExclusive));

        var opening = await BuildAggregateQuery(openingLines, request.GroupByFloatingDetail)
            .ToListAsync(cancellationToken);
        var periodMovement = await BuildAggregateQuery(periodLines, request.GroupByFloatingDetail)
            .ToListAsync(cancellationToken);

        var keys = opening
            .Concat(periodMovement)
            .Select(item => new TrialBalanceKey(item.SubsidiaryAccountId, item.FloatingDetailAccountId))
            .Distinct()
            .ToList();

        if (keys.Count == 0)
        {
            return new TrialBalanceDto
            {
                FiscalPeriodId = period.Id,
                FiscalPeriodName = period.Name,
                StartDate = period.StartDate,
                EndDate = period.EndDate,
                IncludeMoatagh = request.IncludeMoatagh,
                GroupByFloatingDetail = request.GroupByFloatingDetail
            };
        }

        var accountIds = keys
            .Select(item => item.SubsidiaryAccountId)
            .Distinct()
            .ToList();
        var floatingDetailIds = keys
            .Where(item => item.FloatingDetailAccountId.HasValue)
            .Select(item => item.FloatingDetailAccountId!.Value)
            .Distinct()
            .ToList();

        var accounts = await _context.SubsidiaryAccounts
            .AsNoTracking()
            .Where(item => accountIds.Contains(item.Id))
            .Select(item => new AccountLookup(item.Id, item.Code, item.Name))
            .ToDictionaryAsync(item => item.Id, cancellationToken);

        var floatingDetails = floatingDetailIds.Count == 0
            ? new Dictionary<Guid, FloatingDetailLookup>()
            : await _context.FloatingDetailAccounts
                .AsNoTracking()
                .Where(item => floatingDetailIds.Contains(item.Id))
                .Select(item => new FloatingDetailLookup(item.Id, item.Code, item.Name))
                .ToDictionaryAsync(item => item.Id, cancellationToken);

        var openingLookup = opening.ToDictionary(
            item => new TrialBalanceKey(item.SubsidiaryAccountId, item.FloatingDetailAccountId));
        var periodLookup = periodMovement.ToDictionary(
            item => new TrialBalanceKey(item.SubsidiaryAccountId, item.FloatingDetailAccountId));

        var rows = new List<TrialBalanceRowDto>(keys.Count);
        foreach (var key in keys)
        {
            openingLookup.TryGetValue(key, out var openingAggregate);
            periodLookup.TryGetValue(key, out var periodAggregate);

            var openingBalance = NormalizeBalance(
                openingAggregate?.Debit ?? 0m,
                openingAggregate?.Credit ?? 0m);
            var periodDebit = periodAggregate?.Debit ?? 0m;
            var periodCredit = periodAggregate?.Credit ?? 0m;
            var closingBalance = NormalizeBalance(
                openingBalance.Debit + periodDebit,
                openingBalance.Credit + periodCredit);

            if (openingBalance.Debit == 0m &&
                openingBalance.Credit == 0m &&
                periodDebit == 0m &&
                periodCredit == 0m &&
                closingBalance.Debit == 0m &&
                closingBalance.Credit == 0m)
            {
                continue;
            }

            accounts.TryGetValue(key.SubsidiaryAccountId, out var account);
            var floatingDetail = key.FloatingDetailAccountId.HasValue &&
                floatingDetails.TryGetValue(key.FloatingDetailAccountId.Value, out var resolvedFloatingDetail)
                    ? resolvedFloatingDetail
                    : null;

            rows.Add(new TrialBalanceRowDto
            {
                AccountCode = account?.Code ?? key.SubsidiaryAccountId.ToString(),
                AccountName = account?.Name ?? string.Empty,
                FloatingDetailCode = floatingDetail?.Code,
                FloatingDetailName = floatingDetail?.Name,
                OpeningDebit = openingBalance.Debit,
                OpeningCredit = openingBalance.Credit,
                PeriodDebit = periodDebit,
                PeriodCredit = periodCredit,
                ClosingDebit = closingBalance.Debit,
                ClosingCredit = closingBalance.Credit
            });
        }

        rows = rows
            .OrderBy(item => item.AccountCode, StringComparer.Ordinal)
            .ThenBy(item => item.FloatingDetailCode ?? string.Empty, StringComparer.Ordinal)
            .ToList();

        return new TrialBalanceDto
        {
            FiscalPeriodId = period.Id,
            FiscalPeriodName = period.Name,
            StartDate = period.StartDate,
            EndDate = period.EndDate,
            IncludeMoatagh = request.IncludeMoatagh,
            GroupByFloatingDetail = request.GroupByFloatingDetail,
            Rows = rows,
            OpeningDebitTotal = rows.Sum(item => item.OpeningDebit),
            OpeningCreditTotal = rows.Sum(item => item.OpeningCredit),
            PeriodDebitTotal = rows.Sum(item => item.PeriodDebit),
            PeriodCreditTotal = rows.Sum(item => item.PeriodCredit),
            ClosingDebitTotal = rows.Sum(item => item.ClosingDebit),
            ClosingCreditTotal = rows.Sum(item => item.ClosingCredit)
        };
    }

    private static IQueryable<TrialBalanceAggregate> BuildAggregateQuery(
        IQueryable<VoucherLine> lines,
        bool groupByFloatingDetail)
    {
        return groupByFloatingDetail
            ? lines
                .GroupBy(line => new { line.SubsidiaryAccountId, line.FloatingDetailAccountId })
                .Select(group => new TrialBalanceAggregate
                {
                    SubsidiaryAccountId = group.Key.SubsidiaryAccountId,
                    FloatingDetailAccountId = group.Key.FloatingDetailAccountId,
                    Debit = group.Sum(line => line.DebitAmount),
                    Credit = group.Sum(line => line.CreditAmount)
                })
            : lines
                .GroupBy(line => line.SubsidiaryAccountId)
                .Select(group => new TrialBalanceAggregate
                {
                    SubsidiaryAccountId = group.Key,
                    FloatingDetailAccountId = null,
                    Debit = group.Sum(line => line.DebitAmount),
                    Credit = group.Sum(line => line.CreditAmount)
                });
    }

    private static BalancePair NormalizeBalance(decimal debit, decimal credit)
    {
        var net = debit - credit;
        if (net > 0m)
        {
            return new BalancePair(net, 0m);
        }

        if (net < 0m)
        {
            return new BalancePair(0m, Math.Abs(net));
        }

        return new BalancePair(0m, 0m);
    }

    private sealed class TrialBalanceAggregate
    {
        public int SubsidiaryAccountId { get; init; }
        public Guid? FloatingDetailAccountId { get; init; }
        public decimal Debit { get; init; }
        public decimal Credit { get; init; }
    }

    private sealed record TrialBalanceKey(int SubsidiaryAccountId, Guid? FloatingDetailAccountId);
    private sealed record AccountLookup(int Id, string Code, string Name);
    private sealed record FloatingDetailLookup(Guid Id, string Code, string Name);
    private sealed record BalancePair(decimal Debit, decimal Credit);
}
