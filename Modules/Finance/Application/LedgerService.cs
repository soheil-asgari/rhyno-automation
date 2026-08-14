using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Modules.Finance.Domain;
using OfficeAutomation.Modules.Finance.Infrastructure.Persistence;

namespace OfficeAutomation.Modules.Finance.Application;

public sealed class LedgerRequest
{
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public int? SubsidiaryAccountId { get; set; }
    public int? GeneralAccountId { get; set; }
    public int? DetailedAccountId { get; set; }
    public Guid? FloatingDetailAccountId { get; set; }
    public bool IncludeMemo { get; set; }
    public bool IncludeDraft { get; set; }
}

public sealed class LedgerEntryDto
{
    public DateTime VoucherDate { get; set; }
    public string DocumentNumber { get; set; } = string.Empty;
    public int? VoucherNumber { get; set; }
    public string Description { get; set; } = string.Empty;
    public int? SubsidiaryAccountId { get; set; }
    public string? SubsidiaryAccountCode { get; set; }
    public string? SubsidiaryAccountName { get; set; }
    public int? GeneralAccountId { get; set; }
    public string? GeneralAccountCode { get; set; }
    public string? GeneralAccountName { get; set; }
    public int? DetailedAccountId { get; set; }
    public string? DetailedAccountCode { get; set; }
    public string? DetailedAccountName { get; set; }
    public Guid? FloatingDetailAccountId { get; set; }
    public string? FloatingDetailCode { get; set; }
    public string? FloatingDetailName { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public decimal RunningBalance { get; set; }
}

public sealed class LedgerDto
{
    public IReadOnlyList<LedgerEntryDto> Entries { get; set; } = [];
    public decimal OpeningBalance { get; set; }
    public decimal ClosingBalance { get; set; }
    public decimal TotalDebits { get; set; }
    public decimal TotalCredits { get; set; }
}

/// <summary>
/// Produces General, Subsidiary (معین), Detailed (تفصیلی) and Party (طرف حساب) ledgers.
/// All balances are derived consistently from posted, non-memo accounting transactions.
/// </summary>
public sealed class LedgerService
{
    private readonly FinanceDbContext _context;

    public LedgerService(FinanceDbContext context)
    {
        _context = context;
    }

    public async Task<LedgerDto> GetLedgerAsync(
        LedgerRequest request,
        CancellationToken cancellationToken = default)
    {
        var from = request.From?.Date ?? DateTime.MinValue;
        var to = request.To?.Date ?? DateTime.MaxValue;

        System.Linq.Expressions.Expression<Func<VoucherLine, bool>> ApplyScope = line =>
            (!request.SubsidiaryAccountId.HasValue || line.SubsidiaryAccountId == request.SubsidiaryAccountId.Value) &&
            (!request.DetailedAccountId.HasValue || line.DetailedAccountId == request.DetailedAccountId.Value) &&
            (!request.FloatingDetailAccountId.HasValue || line.FloatingDetailAccountId == request.FloatingDetailAccountId.Value) &&
            (!request.GeneralAccountId.HasValue || line.SubsidiaryAccount.GeneralAccountId == request.GeneralAccountId.Value);

        var baseQuery = _context.VoucherLines
            .AsNoTracking()
            .Where(line =>
                (!request.IncludeMemo && !line.VoucherHeader.IsMemo) &&
                (request.IncludeDraft ||
                    line.VoucherHeader.PostingStatus == PostingStatus.Posted ||
                    line.VoucherHeader.Status == VoucherStatus.Permanent) &&
                line.VoucherHeader.VoucherDate >= from &&
                line.VoucherHeader.VoucherDate <= to);

        if (request.SubsidiaryAccountId.HasValue)
        {
            baseQuery = baseQuery.Where(line => line.SubsidiaryAccountId == request.SubsidiaryAccountId.Value);
        }

        if (request.DetailedAccountId.HasValue)
        {
            baseQuery = baseQuery.Where(line => line.DetailedAccountId == request.DetailedAccountId.Value);
        }

        if (request.FloatingDetailAccountId.HasValue)
        {
            baseQuery = baseQuery.Where(line => line.FloatingDetailAccountId == request.FloatingDetailAccountId.Value);
        }

        // For a general-account ledger, scope to all subsidiary accounts under it.
        if (request.GeneralAccountId.HasValue)
        {
            baseQuery = baseQuery.Where(line => line.SubsidiaryAccount.GeneralAccountId == request.GeneralAccountId.Value);
        }

        var lines = await baseQuery
            .OrderBy(line => line.VoucherHeader.VoucherDate)
            .ThenBy(line => line.VoucherHeader.SequenceNumber)
            .ThenBy(line => line.DisplayOrder)
            .Select(line => new
            {
                HeaderId = line.VoucherHeaderId,
                line.VoucherHeader.VoucherDate,
                line.VoucherHeader.DocumentNumber,
                line.VoucherHeader.VoucherNumber,
                line.VoucherHeader.Description,
                line.SubsidiaryAccountId,
                SubsidiaryAccountCode = line.SubsidiaryAccount.Code,
                SubsidiaryAccountName = line.SubsidiaryAccount.Name,
                GeneralAccountId = line.SubsidiaryAccount.GeneralAccountId,
                GeneralAccountCode = line.SubsidiaryAccount.GeneralAccount.Code,
                GeneralAccountName = line.SubsidiaryAccount.GeneralAccount.Name,
                line.DetailedAccountId,
                DetailedAccountCode = line.DetailedAccount != null ? line.DetailedAccount.Code : null,
                DetailedAccountName = line.DetailedAccount != null ? line.DetailedAccount.Name : null,
                line.FloatingDetailAccountId,
                FloatingDetailCode = line.FloatingDetailAccount != null ? line.FloatingDetailAccount.Code : null,
                FloatingDetailName = line.FloatingDetailAccount != null ? line.FloatingDetailAccount.Name : null,
                line.DebitAmount,
                line.CreditAmount
            })
            .ToListAsync(cancellationToken);

        // Compute the opening balance from all posted, non-memo lines strictly before 'from'.
        var openingDebit = await _context.VoucherLines
            .AsNoTracking()
            .Where(line =>
                (!request.IncludeMemo && !line.VoucherHeader.IsMemo) &&
                (request.IncludeDraft ||
                    line.VoucherHeader.PostingStatus == PostingStatus.Posted ||
                    line.VoucherHeader.Status == VoucherStatus.Permanent) &&
                line.VoucherHeader.VoucherDate < from &&
                (request.IncludeDraft ||
                    line.VoucherHeader.Status == VoucherStatus.Permanent))
            .Where(ApplyScope)
            .SumAsync(line => line.DebitAmount, cancellationToken);
        var openingCredit = await _context.VoucherLines
            .AsNoTracking()
            .Where(line =>
                (!request.IncludeMemo && !line.VoucherHeader.IsMemo) &&
                (request.IncludeDraft ||
                    line.VoucherHeader.PostingStatus == PostingStatus.Posted ||
                    line.VoucherHeader.Status == VoucherStatus.Permanent) &&
                line.VoucherHeader.VoucherDate < from &&
                (request.IncludeDraft ||
                    line.VoucherHeader.Status == VoucherStatus.Permanent))
            .Where(ApplyScope)
            .SumAsync(line => line.CreditAmount, cancellationToken);

        var openingBalance = openingDebit - openingCredit;
        var runningBalance = openingBalance;

        var entries = new List<LedgerEntryDto>(lines.Count);
        foreach (var line in lines)
        {
            runningBalance += line.DebitAmount - line.CreditAmount;
            entries.Add(new LedgerEntryDto
            {
                VoucherDate = line.VoucherDate,
                DocumentNumber = line.DocumentNumber,
                VoucherNumber = line.VoucherNumber,
                Description = line.Description ?? string.Empty,
                SubsidiaryAccountId = line.SubsidiaryAccountId,
                SubsidiaryAccountCode = line.SubsidiaryAccountCode,
                SubsidiaryAccountName = line.SubsidiaryAccountName,
                GeneralAccountId = line.GeneralAccountId,
                GeneralAccountCode = line.GeneralAccountCode,
                GeneralAccountName = line.GeneralAccountName,
                DetailedAccountId = line.DetailedAccountId,
                DetailedAccountCode = line.DetailedAccountCode,
                DetailedAccountName = line.DetailedAccountName,
                FloatingDetailAccountId = line.FloatingDetailAccountId,
                FloatingDetailCode = line.FloatingDetailCode,
                FloatingDetailName = line.FloatingDetailName,
                Debit = line.DebitAmount,
                Credit = line.CreditAmount,
                RunningBalance = runningBalance
            });
        }

        return new LedgerDto
        {
            Entries = entries,
            OpeningBalance = openingBalance,
            ClosingBalance = runningBalance,
            TotalDebits = entries.Sum(item => item.Debit),
            TotalCredits = entries.Sum(item => item.Credit)
        };
    }

    private static bool MatchesScope(
        VoucherLine line,
        LedgerRequest request)
    {
        if (request.SubsidiaryAccountId.HasValue && line.SubsidiaryAccountId != request.SubsidiaryAccountId.Value)
        {
            return false;
        }

        if (request.DetailedAccountId.HasValue && line.DetailedAccountId != request.DetailedAccountId.Value)
        {
            return false;
        }

        if (request.FloatingDetailAccountId.HasValue && line.FloatingDetailAccountId != request.FloatingDetailAccountId.Value)
        {
            return false;
        }

        if (request.GeneralAccountId.HasValue && line.SubsidiaryAccount.GeneralAccountId != request.GeneralAccountId.Value)
        {
            return false;
        }

        return true;
    }
}