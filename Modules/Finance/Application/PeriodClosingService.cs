using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Modules.Finance.Domain;
using OfficeAutomation.Modules.Finance.Infrastructure.Persistence;

namespace OfficeAutomation.Modules.Finance.Application;

public sealed class PeriodClosingService
{
    private readonly FinanceDbContext _context;
    private readonly VoucherValidationGuard _voucherValidationGuard;

    public PeriodClosingService(FinanceDbContext context)
    {
        _context = context;
        _voucherValidationGuard = new VoucherValidationGuard(context);
    }

    public async Task<VoucherHeader> CloseTemporaryAccountsAsync(
        Guid fiscalPeriodId,
        int destinationAccountId,
        CancellationToken cancellationToken = default)
    {
        if (fiscalPeriodId == Guid.Empty)
        {
            throw new InvalidOperationException("Fiscal period id is required.");
        }

        if (destinationAccountId <= 0)
        {
            throw new InvalidOperationException("Destination account id is required.");
        }

        var period = await _context.FiscalPeriods
            .Include(item => item.FiscalYear)
            .FirstOrDefaultAsync(item => item.Id == fiscalPeriodId, cancellationToken)
            ?? throw new InvalidOperationException($"Fiscal period '{fiscalPeriodId}' was not found.");

        if (period.Status == FiscalPeriodStatus.HardLocked)
        {
            throw new InvalidOperationException($"Fiscal period '{period.Name}' is hard locked and cannot be closed.");
        }

        var destinationAccount = await _context.SubsidiaryAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == destinationAccountId && item.IsActive, cancellationToken)
            ?? throw new InvalidOperationException("Destination account is invalid or inactive.");

        if (destinationAccount.IsTemporary)
        {
            throw new InvalidOperationException("Destination account for closing must not be temporary.");
        }

        var documentNumber = $"CLOSE-TEMP-{period.FiscalYear.YearName}-{period.PeriodNumber:00}";
        var existing = await _context.VoucherHeaders
            .Include(item => item.Lines)
            .FirstOrDefaultAsync(item => item.DocumentNumber == documentNumber, cancellationToken);
        if (existing != null)
        {
            return existing;
        }

        var balances = await _context.VoucherLines
            .AsNoTracking()
            .Where(line =>
                line.VoucherHeader.FiscalYearId == period.FiscalYearId &&
                line.VoucherHeader.VoucherDate >= period.StartDate.Date &&
                line.VoucherHeader.VoucherDate <= period.EndDate.Date &&
                line.VoucherHeader.TotalDebits == line.VoucherHeader.TotalCredits &&
                line.SubsidiaryAccount.IsActive &&
                line.SubsidiaryAccount.IsTemporary)
            .GroupBy(line => new
            {
                line.SubsidiaryAccountId,
                line.SubsidiaryAccount.Code,
                line.SubsidiaryAccount.Name
            })
            .Select(group => new TemporaryBalanceRow(
                group.Key.SubsidiaryAccountId,
                group.Key.Code,
                group.Key.Name,
                group.Sum(item => item.DebitAmount),
                group.Sum(item => item.CreditAmount)))
            .ToListAsync(cancellationToken);

        var closingLines = new List<VoucherLine>();
        var displayOrder = 1;

        foreach (var balance in balances)
        {
            var net = balance.Debit - balance.Credit;
            if (net == 0m)
            {
                continue;
            }

            closingLines.Add(new VoucherLine
            {
                SubsidiaryAccountId = balance.SubsidiaryAccountId,
                DebitAmount = net < 0m ? Math.Abs(net) : 0m,
                CreditAmount = net > 0m ? net : 0m,
                Narration = $"Closing temporary account {balance.AccountCode} - {balance.AccountName}",
                DisplayOrder = displayOrder++
            });
        }

        if (closingLines.Count == 0)
        {
            throw new InvalidOperationException($"Fiscal period '{period.Name}' has no temporary account balances to close.");
        }

        var totalDebit = closingLines.Sum(item => item.DebitAmount);
        var totalCredit = closingLines.Sum(item => item.CreditAmount);
        var difference = totalDebit - totalCredit;
        if (difference != 0m)
        {
            closingLines.Add(new VoucherLine
            {
                SubsidiaryAccountId = destinationAccountId,
                DebitAmount = difference < 0m ? Math.Abs(difference) : 0m,
                CreditAmount = difference > 0m ? difference : 0m,
                Narration = $"Summary profit and loss transfer for {period.Name}",
                DisplayOrder = displayOrder
            });
        }

        var journalType = await _context.JournalTypes
            .FirstOrDefaultAsync(item => item.Code == JournalTypeCodes.Closing, cancellationToken)
            ?? throw new InvalidOperationException("Closing journal type is not configured.");

        var voucher = new VoucherHeader
        {
            DocumentNumber = documentNumber,
            VoucherDate = period.EndDate.Date,
            Description = $"بابت بستن حساب‌های موقت دوره مالی {period.Name}",
            Status = VoucherStatus.Draft,
            PostingStatus = PostingStatus.Draft,
            FiscalYearId = period.FiscalYearId,
            JournalTypeId = journalType.Id,
            Lines = closingLines
        };

        voucher.TotalDebits = voucher.Lines.Sum(item => item.DebitAmount);
        voucher.TotalCredits = voucher.Lines.Sum(item => item.CreditAmount);
        if (voucher.TotalDebits != voucher.TotalCredits)
        {
            throw new InvalidOperationException("Closing voucher is not balanced.");
        }

        await _voucherValidationGuard.ValidateAsync(voucher, cancellationToken);

        voucher.Status = VoucherStatus.Reviewed;
        _context.VoucherHeaders.Add(voucher);
        await _context.SaveChangesAsync(cancellationToken);
        return voucher;
    }

    private sealed record TemporaryBalanceRow(
        int SubsidiaryAccountId,
        string AccountCode,
        string AccountName,
        decimal Debit,
        decimal Credit);
}
