using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Modules.Finance.Application;
using OfficeAutomation.Modules.Finance.Domain;
using OfficeAutomation.Modules.Finance.Infrastructure.Persistence;
using Xunit;

namespace OfficeAutomation.Tests;

public sealed class TrialBalanceServiceTests
{
    [Fact]
    public async Task GetTrialBalance_ExcludesMoataghVouchersByDefault()
    {
        using var context = CreateFinanceContext(nameof(GetTrialBalance_ExcludesMoataghVouchersByDefault));
        var seed = await SeedTrialBalanceScenarioAsync(context);
        var service = new TrialBalanceService(context);

        var report = await service.GetTrialBalanceAsync(seed.PeriodId, includeMoatagh: false, groupByFloatingDetail: false);

        var cash = Assert.Single(report.Rows, item => item.AccountCode == "110801");
        var clearing = Assert.Single(report.Rows, item => item.AccountCode == "210801");
        Assert.Equal(1_000m, cash.OpeningDebit);
        Assert.Equal(200m, cash.PeriodDebit);
        Assert.Equal(1_200m, cash.ClosingDebit);
        Assert.Equal(1_000m, clearing.OpeningCredit);
        Assert.Equal(200m, clearing.PeriodCredit);
        Assert.Equal(1_200m, clearing.ClosingCredit);
        AssertBalanced(report);
    }

    [Fact]
    public async Task GetTrialBalance_IncludesMoataghVouchersWhenRequested()
    {
        using var context = CreateFinanceContext(nameof(GetTrialBalance_IncludesMoataghVouchersWhenRequested));
        var seed = await SeedTrialBalanceScenarioAsync(context);
        var service = new TrialBalanceService(context);

        var report = await service.GetTrialBalanceAsync(seed.PeriodId, includeMoatagh: true, groupByFloatingDetail: false);

        var cash = Assert.Single(report.Rows, item => item.AccountCode == "110801");
        var clearing = Assert.Single(report.Rows, item => item.AccountCode == "210801");
        Assert.Equal(350m, cash.PeriodDebit);
        Assert.Equal(1_350m, cash.ClosingDebit);
        Assert.Equal(350m, clearing.PeriodCredit);
        Assert.Equal(1_350m, clearing.ClosingCredit);
        AssertBalanced(report);
    }

    [Fact]
    public async Task GetTrialBalance_GroupsByFloatingDetailWhenRequested()
    {
        using var context = CreateFinanceContext(nameof(GetTrialBalance_GroupsByFloatingDetailWhenRequested));
        var seed = await SeedTrialBalanceScenarioAsync(context);
        var service = new TrialBalanceService(context);

        var report = await service.GetTrialBalanceAsync(seed.PeriodId, includeMoatagh: false, groupByFloatingDetail: true);

        var detailRow = Assert.Single(report.Rows, item => item.FloatingDetailCode == "CUST-801");
        Assert.Equal("110801", detailRow.AccountCode);
        Assert.Equal(1_000m, detailRow.OpeningDebit);
        Assert.Equal(200m, detailRow.PeriodDebit);
        Assert.Equal(1_200m, detailRow.ClosingDebit);
        AssertBalanced(report);
    }

    [Fact]
    public async Task GetTrialBalance_UsesPrePeriodVouchersForOpeningBalance()
    {
        using var context = CreateFinanceContext(nameof(GetTrialBalance_UsesPrePeriodVouchersForOpeningBalance));
        var seed = await SeedTrialBalanceScenarioAsync(context);
        var service = new TrialBalanceService(context);

        var report = await service.GetTrialBalanceAsync(seed.PeriodId, includeMoatagh: false, groupByFloatingDetail: false);

        var cash = Assert.Single(report.Rows, item => item.AccountCode == "110801");
        var clearing = Assert.Single(report.Rows, item => item.AccountCode == "210801");
        Assert.Equal(1_000m, cash.OpeningDebit);
        Assert.Equal(1_000m, clearing.OpeningCredit);
        AssertBalanced(report);
    }

    private static FinanceDbContext CreateFinanceContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<FinanceDbContext>()
            .UseInMemoryDatabase(databaseName)
            .EnableSensitiveDataLogging()
            .Options;

        return new FinanceDbContext(options);
    }

    private static async Task<TrialBalanceSeed> SeedTrialBalanceScenarioAsync(FinanceDbContext context)
    {
        var fiscalYear = new FiscalYear
        {
            YearName = "2035",
            StartDate = new DateTime(2035, 1, 1),
            EndDate = new DateTime(2035, 12, 31)
        };
        var period = new FiscalPeriod
        {
            FiscalYear = fiscalYear,
            Name = "2035-01",
            PeriodNumber = 1,
            StartDate = new DateTime(2035, 1, 1),
            EndDate = new DateTime(2035, 1, 31),
            Status = FiscalPeriodStatus.Open
        };
        var openingJournal = new JournalType { Code = JournalTypeCodes.Opening, Name = "Opening" };
        var generalJournal = new JournalType { Code = JournalTypeCodes.General, Name = "General" };
        var assets = new AccountGroup { Code = "1", Name = "Assets", Nature = AccountNature.Debit };
        var liabilities = new AccountGroup { Code = "2", Name = "Liabilities", Nature = AccountNature.Credit };
        var cash = new SubsidiaryAccount
        {
            Code = "110801",
            Name = "Cash",
            SystemKey = "TB_CASH",
            GeneralAccount = new GeneralAccount { Code = "110", Name = "Cash", AccountGroup = assets }
        };
        var clearing = new SubsidiaryAccount
        {
            Code = "210801",
            Name = "Clearing",
            SystemKey = "TB_CLEARING",
            AllowsFloatingDetail = false,
            GeneralAccount = new GeneralAccount { Code = "210", Name = "Clearing", AccountGroup = liabilities }
        };
        var customer = new FloatingDetailAccount
        {
            Code = "CUST-801",
            Name = "Customer 801",
            Type = FloatingDetailAccountType.Person
        };

        context.AddRange(fiscalYear, period, openingJournal, generalJournal, cash, clearing, customer);
        context.SubsidiaryAccountFloatingDetails.Add(new SubsidiaryAccountFloatingDetail
        {
            SubsidiaryAccount = cash,
            FloatingDetailAccount = customer
        });
        await context.SaveChangesAsync();

        context.VoucherHeaders.AddRange(
            CreateVoucher("TB-OPEN", new DateTime(2035, 1, 1), VoucherStatus.Permanent, openingJournal.Id, fiscalYear.Id, cash.Id, clearing.Id, customer.Id, 600m),
            CreateVoucher("TB-PRE", new DateTime(2034, 12, 29), VoucherStatus.Permanent, generalJournal.Id, fiscalYear.Id, cash.Id, clearing.Id, customer.Id, 400m),
            CreateVoucher("TB-PERM", new DateTime(2035, 1, 10), VoucherStatus.Permanent, generalJournal.Id, fiscalYear.Id, cash.Id, clearing.Id, customer.Id, 200m),
            CreateVoucher("TB-DRAFT", new DateTime(2035, 1, 11), VoucherStatus.Draft, generalJournal.Id, fiscalYear.Id, cash.Id, clearing.Id, customer.Id, 30m),
            CreateVoucher("TB-REVIEW", new DateTime(2035, 1, 12), VoucherStatus.Reviewed, generalJournal.Id, fiscalYear.Id, cash.Id, clearing.Id, customer.Id, 50m),
            CreateVoucher("TB-APPROVE", new DateTime(2035, 1, 13), VoucherStatus.Approved, generalJournal.Id, fiscalYear.Id, cash.Id, clearing.Id, customer.Id, 70m),
            CreateUnbalancedVoucher("TB-UNBALANCED-DRAFT", new DateTime(2035, 1, 14), generalJournal.Id, fiscalYear.Id, cash.Id, 999m));
        await context.SaveChangesAsync();

        return new TrialBalanceSeed(period.Id);
    }

    private static VoucherHeader CreateVoucher(
        string documentNumber,
        DateTime voucherDate,
        VoucherStatus status,
        int journalTypeId,
        int fiscalYearId,
        int debitAccountId,
        int creditAccountId,
        Guid floatingDetailAccountId,
        decimal amount)
    {
        return new VoucherHeader
        {
            DocumentNumber = documentNumber,
            VoucherDate = voucherDate,
            Status = status,
            PostingStatus = status == VoucherStatus.Permanent ? PostingStatus.Posted : PostingStatus.Draft,
            FiscalYearId = fiscalYearId,
            JournalTypeId = journalTypeId,
            TotalDebits = amount,
            TotalCredits = amount,
            Lines =
            [
                new VoucherLine
                {
                    SubsidiaryAccountId = debitAccountId,
                    FloatingDetailAccountId = floatingDetailAccountId,
                    DebitAmount = amount,
                    CreditAmount = 0m,
                    ExchangeRate = 1m,
                    DisplayOrder = 1
                },
                new VoucherLine
                {
                    SubsidiaryAccountId = creditAccountId,
                    DebitAmount = 0m,
                    CreditAmount = amount,
                    ExchangeRate = 1m,
                    DisplayOrder = 2
                }
            ]
        };
    }

    private static VoucherHeader CreateUnbalancedVoucher(
        string documentNumber,
        DateTime voucherDate,
        int journalTypeId,
        int fiscalYearId,
        int debitAccountId,
        decimal amount)
    {
        return new VoucherHeader
        {
            DocumentNumber = documentNumber,
            VoucherDate = voucherDate,
            Status = VoucherStatus.Draft,
            PostingStatus = PostingStatus.Draft,
            FiscalYearId = fiscalYearId,
            JournalTypeId = journalTypeId,
            TotalDebits = amount,
            TotalCredits = 0m,
            Lines =
            [
                new VoucherLine
                {
                    SubsidiaryAccountId = debitAccountId,
                    DebitAmount = amount,
                    CreditAmount = 0m,
                    ExchangeRate = 1m,
                    DisplayOrder = 1
                }
            ]
        };
    }

    private static void AssertBalanced(TrialBalanceDto report)
    {
        Assert.Equal(report.OpeningDebitTotal, report.OpeningCreditTotal);
        Assert.Equal(report.PeriodDebitTotal, report.PeriodCreditTotal);
        Assert.Equal(report.ClosingDebitTotal, report.ClosingCreditTotal);
        Assert.Equal(report.Rows.Sum(item => item.OpeningDebit), report.Rows.Sum(item => item.OpeningCredit));
        Assert.Equal(report.Rows.Sum(item => item.PeriodDebit), report.Rows.Sum(item => item.PeriodCredit));
        Assert.Equal(report.Rows.Sum(item => item.ClosingDebit), report.Rows.Sum(item => item.ClosingCredit));
    }

    private sealed record TrialBalanceSeed(Guid PeriodId);
}
