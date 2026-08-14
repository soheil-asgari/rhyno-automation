using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Models;
using OfficeAutomation.Modules.Finance.Application;
using OfficeAutomation.Modules.Finance.Domain;
using OfficeAutomation.Modules.Finance.Infrastructure.Persistence;
using Xunit;

namespace OfficeAutomation.Tests;

public sealed class AccountingCoreTests
{
    // ── Debit / Credit equality ─────────────────────────────────────────────
    [Fact]
    public async Task CreateManualVoucher_RejectsUnbalancedVoucher()
    {
        using var context = CreateFinanceContext(nameof(CreateManualVoucher_RejectsUnbalancedVoucher));
        var seed = await SeedBasicScenarioAsync(context, year: 2040);
        var service = new FinanceLedgerService(context);

        var model = new SimpleVoucherCreateVM
        {
            VoucherDate = new DateTime(2040, 1, 15),
            JournalTypeId = seed.JournalTypeId,
            Description = "unbalanced",
            Lines =
            [
                new SimpleVoucherLineVM { SubsidiaryAccountId = seed.DebitAccountId, DebitAmount = 100m, CreditAmount = 0m },
                new SimpleVoucherLineVM { SubsidiaryAccountId = seed.CreditAccountId, DebitAmount = 0m, CreditAmount = 90m }
            ]
        };

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateManualVoucherAsync(model));
        Assert.Contains("تراز نیست", error.Message);
    }

    // ── Posting ─────────────────────────────────────────────────────────────
    [Fact]
    public async Task PostVoucher_PostsBalancedVoucherAndMakesItImmutable()
    {
        using var context = CreateFinanceContext(nameof(PostVoucher_PostsBalancedVoucherAndMakesItImmutable));
        var seed = await SeedBasicScenarioAsync(context, year: 2041);
        var service = new FinanceLedgerService(context);
        var draft = await service.CreateManualVoucherAsync(BuildBalancedModel(seed.JournalTypeId, seed.DebitAccountId, seed.CreditAccountId, seed.FloatingDetailId, 250m, 2041));

        var posted = await service.PostVoucherAsync(draft.Id);

        var persisted = await context.VoucherHeaders.AsNoTracking().SingleAsync(item => item.Id == posted.Id);
        Assert.Equal(VoucherStatus.Permanent, persisted.Status);
        Assert.Equal(PostingStatus.Posted, persisted.PostingStatus);

        // `posted` is the tracked instance already in the context in the Permanent/Posted state.
        posted.Description = "tamper";
        context.Entry(posted).Property(item => item.Description).IsModified = true;
        var guardError = await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());
        Assert.Contains("immutable", guardError.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ── Memo exclusion from ledger / trial balance ─────────────────────────
    [Fact]
    public async Task MemoVoucher_IsExcludedFromLedgerAndTrialBalance()
    {
        using var context = CreateFinanceContext(nameof(MemoVoucher_IsExcludedFromLedgerAndTrialBalance));
        var seed = await SeedBasicScenarioAsync(context, year: 2042);
        var service = new FinanceLedgerService(context);

        // Post one regular permanent voucher.
        var posted = await service.CreateManualVoucherAsync(BuildBalancedModel(seed.JournalTypeId, seed.DebitAccountId, seed.CreditAccountId, seed.FloatingDetailId, 100m, 2042));
        await service.PostVoucherAsync(posted.Id);

        // Insert a memo voucher (draft + IsMemo) that must not affect balances.
        var memo = new VoucherHeader
        {
            DocumentNumber = $"MEMO-{Guid.NewGuid():N}"[..24],
            VoucherDate = new DateTime(2042, 1, 20),
            Description = "memo only",
            Status = VoucherStatus.Draft,
            PostingStatus = PostingStatus.Draft,
            IsMemo = true,
            FiscalYearId = seed.FiscalYearId,
            JournalTypeId = seed.JournalTypeId,
            TotalDebits = 500m,
            TotalCredits = 500m,
            Lines =
            [
                new VoucherLine { SubsidiaryAccountId = seed.DebitAccountId, DebitAmount = 500m, CreditAmount = 0m, ExchangeRate = 1m, DisplayOrder = 1 },
                new VoucherLine { SubsidiaryAccountId = seed.CreditAccountId, DebitAmount = 0m, CreditAmount = 500m, ExchangeRate = 1m, DisplayOrder = 2 }
            ]
        };
        context.VoucherHeaders.Add(memo);
        await context.SaveChangesAsync();

        // Ledger scope for the debit account.
        var ledger = new LedgerService(context);
        var ledgerReport = await ledger.GetLedgerAsync(new LedgerRequest
        {
            SubsidiaryAccountId = seed.DebitAccountId,
            From = new DateTime(2042, 1, 1),
            To = new DateTime(2042, 1, 31)
        });

        // Only the 100 posted shows; the 500 memo is excluded.
        Assert.Equal(100m, ledgerReport.TotalDebits);
        Assert.Equal(100m, ledgerReport.ClosingBalance);

        // Trial balance also excludes the memo.
        var period = await context.FiscalPeriods.SingleAsync(item => item.FiscalYearId == seed.FiscalYearId);
        var trial = new TrialBalanceService(context);
        var report = await trial.GetTrialBalanceAsync(period.Id, includeMoatagh: true, groupByFloatingDetail: false);
        var debitRow = Assert.Single(report.Rows, item => item.AccountCode == seed.DebitCode);
        Assert.Equal(100m, debitRow.PeriodDebit);
    }

    // ── Reversal ────────────────────────────────────────────────────────────
    [Fact]
    public async Task ReverseVoucher_CreatesBalancedReversal_OriginalUntouched()
    {
        using var context = CreateFinanceContext(nameof(ReverseVoucher_CreatesBalancedReversal_OriginalUntouched));
        var seed = await SeedBasicScenarioAsync(context, year: 2043);
        var service = new FinanceLedgerService(context);
        var posted = await service.CreateManualVoucherAsync(BuildBalancedModel(seed.JournalTypeId, seed.DebitAccountId, seed.CreditAccountId, seed.FloatingDetailId, 300m, 2043));
        await service.PostVoucherAsync(posted.Id);

        var reversal = await service.ReverseVoucherAsync(posted.Id, "test reversal", new DateTime(2043, 2, 1));

        var original = await context.VoucherHeaders.Include(item => item.Lines).SingleAsync(item => item.Id == posted.Id);
        // Original figures are unchanged.
        Assert.Equal(300m, original.TotalDebits);
        Assert.Equal(300m, original.TotalCredits);
        Assert.Equal(1, original.Lines.Count(line => line.DebitAmount > 0));

        Assert.Equal(VoucherStatus.Permanent, reversal.Status);
        Assert.Equal(PostingStatus.Posted, reversal.PostingStatus);
        Assert.Equal(posted.Id, reversal.ReversalOfVoucherHeaderId);
        Assert.Equal(300m, reversal.TotalDebits);
        Assert.Equal(300m, reversal.TotalCredits);
        Assert.Equal(300m, reversal.Lines.Sum(line => line.DebitAmount));
        Assert.Equal(300m, reversal.Lines.Sum(line => line.CreditAmount));
    }

    // ── Copy ────────────────────────────────────────────────────────────────
    [Fact]
    public async Task CopyVoucher_CreatesBalancedDraft_DoesNotModifyOriginal()
    {
        using var context = CreateFinanceContext(nameof(CopyVoucher_CreatesBalancedDraft_DoesNotModifyOriginal));
        var seed = await SeedBasicScenarioAsync(context, year: 2044);
        var service = new FinanceLedgerService(context);
        var original = await service.CreateManualVoucherAsync(BuildBalancedModel(seed.JournalTypeId, seed.DebitAccountId, seed.CreditAccountId, seed.FloatingDetailId, 400m, 2044));

        var copy = await service.CopyVoucherAsync(original.Id, new DateTime(2044, 1, 25), "Copy of 2044", null, swapDebitCredit: true);

        Assert.Equal(VoucherStatus.Draft, copy.Status);
        Assert.Equal(PostingStatus.Draft, copy.PostingStatus);
        Assert.True(copy.IsBalanced());
        // swap flips side: now debit the credit-account.
        Assert.Equal(400m, copy.Lines.Sum(line => line.DebitAmount));
        Assert.Equal(400m, copy.Lines.Sum(line => line.CreditAmount));

        var persistedOriginal = await context.VoucherHeaders.SingleAsync(item => item.Id == original.Id);
        Assert.Equal(VoucherStatus.Draft, persistedOriginal.Status);
        Assert.Equal(400m, persistedOriginal.TotalDebits);
        Assert.Equal(400m, persistedOriginal.TotalCredits);
    }

    // ── Voucher numbering ───────────────────────────────────────────────────
    [Fact]
    public async Task VoucherNumbering_AssignsSequentialNumbers_AcrossDrafts()
    {
        using var context = CreateFinanceContext(nameof(VoucherNumbering_AssignsSequentialNumbers_AcrossDrafts));
        var seed = await SeedBasicScenarioAsync(context, year: 2045);
        var service = new FinanceLedgerService(context);

        var v1 = await service.CreateManualVoucherAsync(BuildBalancedModel(seed.JournalTypeId, seed.DebitAccountId, seed.CreditAccountId, seed.FloatingDetailId, 10m, 2045));
        var v2 = await service.CreateManualVoucherAsync(BuildBalancedModel(seed.JournalTypeId, seed.DebitAccountId, seed.CreditAccountId, seed.FloatingDetailId, 20m, 2045));
        var v3 = await service.CreateManualVoucherAsync(BuildBalancedModel(seed.JournalTypeId, seed.DebitAccountId, seed.CreditAccountId, seed.FloatingDetailId, 30m, 2045));

        Assert.True(v1.SequenceNumber > 0);
        Assert.Equal(v1.SequenceNumber + 1, v2.SequenceNumber);
        Assert.Equal(v2.SequenceNumber + 1, v3.SequenceNumber);
        Assert.Equal(v1.SequenceNumber, v1.VoucherNumber);
    }

    // ── Closed fiscal period ────────────────────────────────────────────────
    [Fact]
    public async Task CreateManualVoucher_RejectsClosedFiscalYear()
    {
        using var context = CreateFinanceContext(nameof(CreateManualVoucher_RejectsClosedFiscalYear));
        var seed = await SeedBasicScenarioAsync(context, year: 2046, closedYear: true);
        var service = new FinanceLedgerService(context);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateManualVoucherAsync(BuildBalancedModel(seed.JournalTypeId, seed.DebitAccountId, seed.CreditAccountId, seed.FloatingDetailId, 50m, 2046)));
        Assert.Contains("سال مالی", error.Message);
    }

    // ── Opening / Carry-forward ─────────────────────────────────────────────
    [Fact]
    public async Task CarryForwardBalances_MovesNetPermanentBalances()
    {
        using var context = CreateFinanceContext(nameof(CarryForwardBalances_MovesNetPermanentBalances));
        var seed = await SeedBasicScenarioAsync(context, year: 2047);
        var service = new FinanceLedgerService(context);
        var posted = await service.CreateManualVoucherAsync(BuildBalancedModel(seed.JournalTypeId, seed.DebitAccountId, seed.CreditAccountId, seed.FloatingDetailId, 600m, 2047));
        await service.PostVoucherAsync(posted.Id);

        // Create next year open.
        var nextYear = new FiscalYear
        {
            YearName = "2048",
            StartDate = new DateTime(2048, 1, 1),
            EndDate = new DateTime(2048, 12, 31)
        };
        context.FiscalYears.Add(nextYear);
        await context.SaveChangesAsync();

        var opening = await service.CarryForwardBalancesAsync(seed.FiscalYearId, nextYear.Id);

        Assert.Equal(VoucherStatus.Permanent, opening.Status);
        Assert.Equal(PostingStatus.Posted, opening.PostingStatus);
        Assert.True(opening.IsBalanced());
        Assert.Equal(600m, opening.TotalDebits);
        Assert.Equal(600m, opening.TotalCredits);
    }

    private static SimpleVoucherCreateVM BuildBalancedModel(
        int journalTypeId,
        int debitAccountId,
        int creditAccountId,
        Guid? floatingDetailId,
        decimal amount,
        int year)
    {
        return new SimpleVoucherCreateVM
        {
            VoucherDate = new DateTime(year, 1, 15),
            JournalTypeId = journalTypeId,
            Description = "balanced test",
            Lines =
            [
                new SimpleVoucherLineVM { SubsidiaryAccountId = debitAccountId, FloatingDetailAccountId = floatingDetailId, DebitAmount = amount, CreditAmount = 0m, ExchangeRate = 1m },
                new SimpleVoucherLineVM { SubsidiaryAccountId = creditAccountId, DebitAmount = 0m, CreditAmount = amount, ExchangeRate = 1m }
            ]
        };
    }

    private static FinanceDbContext CreateFinanceContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<FinanceDbContext>()
            .UseInMemoryDatabase(databaseName)
            .EnableSensitiveDataLogging()
            .Options;

        return new FinanceDbContext(options);
    }

    private static async Task<AccountingSeed> SeedBasicScenarioAsync(
        FinanceDbContext context,
        int year,
        bool closedYear = false)
    {
        var fiscalYear = new FiscalYear
        {
            YearName = year.ToString(),
            StartDate = new DateTime(year, 1, 1),
            EndDate = new DateTime(year, 12, 31),
            IsClosed = closedYear
        };
        var period = new FiscalPeriod
        {
            FiscalYear = fiscalYear,
            Name = $"{year}-01",
            PeriodNumber = 1,
            StartDate = new DateTime(year, 1, 1),
            EndDate = new DateTime(year, 1, 31),
            Status = FiscalPeriodStatus.Open
        };
        var generalJournal = new JournalType
        {
            Code = JournalTypeCodes.General,
            Name = "General",
            IsActive = true
        };
        var assets = new AccountGroup { Code = "1", Name = "Assets", Nature = AccountNature.Debit };
        var liabilities = new AccountGroup { Code = "2", Name = "Liabilities", Nature = AccountNature.Credit };
        var debitGeneral = new GeneralAccount { Code = "110", Name = "Assets Account", AccountGroup = assets, Nature = AccountNature.Debit };
        var creditGeneral = new GeneralAccount { Code = "210", Name = "Liabilities Account", AccountGroup = liabilities, Nature = AccountNature.Credit };
        var debitAccount = new SubsidiaryAccount
        {
            Code = "110801",
            Name = "Cash",
            SystemKey = "ACC_CASH",
            GeneralAccount = debitGeneral,
            Nature = AccountNature.Debit,
            IsActive = true
        };
        var creditAccount = new SubsidiaryAccount
        {
            Code = "210801",
            Name = "Payable",
            SystemKey = "ACC_PAY",
            GeneralAccount = creditGeneral,
            Nature = AccountNature.Credit,
            IsActive = true,
            AllowsFloatingDetail = false
        };
        var floating = new FloatingDetailAccount
        {
            Code = "PRJ-ACC",
            Name = "Project",
            Type = FloatingDetailAccountType.Person,
            IsActive = true
        };

        context.AddRange(fiscalYear, period, generalJournal, debitAccount, creditAccount, floating);
        context.SubsidiaryAccountFloatingDetails.Add(new SubsidiaryAccountFloatingDetail
        {
            SubsidiaryAccount = debitAccount,
            FloatingDetailAccount = floating
        });
        await context.SaveChangesAsync();

        return new AccountingSeed(fiscalYear.Id, generalJournal.Id, period.Id, debitAccount.Id, creditAccount.Id, debitAccount.Code, creditAccount.Code, floating.Id);
    }

    private sealed record AccountingSeed(
        int FiscalYearId,
        int JournalTypeId,
        Guid PeriodId,
        int DebitAccountId,
        int CreditAccountId,
        string DebitCode,
        string CreditCode,
        Guid FloatingDetailId);
}