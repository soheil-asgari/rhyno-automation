using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Models;
using OfficeAutomation.Modules.Finance.Application;
using OfficeAutomation.Modules.Finance.Domain;
using OfficeAutomation.Modules.Finance.Infrastructure.Persistence;
using Xunit;

namespace OfficeAutomation.Tests;

public sealed class VoucherValidationGuardTests
{
    [Fact]
    public async Task CreateManualVoucher_BlocksInactiveSubsidiaryAccount()
    {
        using var context = CreateFinanceContext(nameof(CreateManualVoucher_BlocksInactiveSubsidiaryAccount));
        var seed = await SeedScenarioAsync(context, deactivateCash: true);
        var service = new FinanceLedgerService(context);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateManualVoucherAsync(
            BuildVoucherModel(seed.JournalTypeId, seed.CashAccountId, seed.PayableAccountId, seed.FloatingDetailId, 250m)));

        Assert.Contains("حساب معین غیرفعال", error.Message);
    }

    [Fact]
    public async Task CreateManualVoucher_BlocksInactiveFloatingDetail()
    {
        using var context = CreateFinanceContext(nameof(CreateManualVoucher_BlocksInactiveFloatingDetail));
        var seed = await SeedScenarioAsync(context, deactivateFloatingDetail: true);
        var service = new FinanceLedgerService(context);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateManualVoucherAsync(
            BuildVoucherModel(seed.JournalTypeId, seed.CashAccountId, seed.PayableAccountId, seed.FloatingDetailId, 250m)));

        Assert.Contains("تفصیلی شناور غیرفعال", error.Message);
    }

    [Fact]
    public async Task ChangeVoucherStatus_BlocksInactiveAccountInDraftVoucher()
    {
        using var context = CreateFinanceContext(nameof(ChangeVoucherStatus_BlocksInactiveAccountInDraftVoucher));
        var seed = await SeedScenarioAsync(context);
        var service = new FinanceLedgerService(context);

        var voucher = await service.CreateManualVoucherAsync(
            BuildVoucherModel(seed.JournalTypeId, seed.CashAccountId, seed.PayableAccountId, seed.FloatingDetailId, 250m));

        var cash = await context.SubsidiaryAccounts.SingleAsync(item => item.Id == seed.CashAccountId);
        cash.IsActive = false;
        await context.SaveChangesAsync();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ChangeVoucherStatusAsync(voucher.Id, VoucherStatus.Reviewed));

        Assert.Contains("حساب معین غیرفعال", error.Message);
    }

    [Fact]
    public async Task CreateManualVoucher_BlocksDebitNatureAccountFromGoingCredit()
    {
        using var context = CreateFinanceContext(nameof(CreateManualVoucher_BlocksDebitNatureAccountFromGoingCredit));
        var seed = await SeedScenarioAsync(context, existingCashBalance: 100m);
        var service = new FinanceLedgerService(context);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateManualVoucherAsync(
            BuildVoucherModel(seed.JournalTypeId, seed.PayableAccountId, seed.CashAccountId, null, 180m)));

        Assert.Contains("حساب بدهکار اصیل", error.Message);
    }

    private static SimpleVoucherCreateVM BuildVoucherModel(
        int journalTypeId,
        int debitAccountId,
        int creditAccountId,
        Guid? floatingDetailId,
        decimal amount)
    {
        return new SimpleVoucherCreateVM
        {
            VoucherDate = new DateTime(2036, 1, 15),
            JournalTypeId = journalTypeId,
            Description = "manual validation test",
            Lines =
            [
                new SimpleVoucherLineVM
                {
                    SubsidiaryAccountId = debitAccountId,
                    FloatingDetailAccountId = floatingDetailId,
                    DebitAmount = amount,
                    CreditAmount = 0m,
                    ExchangeRate = 1m
                },
                new SimpleVoucherLineVM
                {
                    SubsidiaryAccountId = creditAccountId,
                    DebitAmount = 0m,
                    CreditAmount = amount,
                    ExchangeRate = 1m
                }
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

    private static async Task<ValidationSeed> SeedScenarioAsync(
        FinanceDbContext context,
        bool deactivateCash = false,
        bool deactivateFloatingDetail = false,
        decimal existingCashBalance = 0m)
    {
        var fiscalYear = new FiscalYear
        {
            YearName = "2036",
            StartDate = new DateTime(2036, 1, 1),
            EndDate = new DateTime(2036, 12, 31)
        };
        var generalJournal = new JournalType
        {
            Code = JournalTypeCodes.General,
            Name = "General",
            IsActive = true
        };
        var assets = new AccountGroup { Code = "1", Name = "Assets", Nature = AccountNature.Debit };
        var liabilities = new AccountGroup { Code = "2", Name = "Liabilities", Nature = AccountNature.Credit };
        var cashGeneral = new GeneralAccount { Code = "110", Name = "Cash", AccountGroup = assets, Nature = AccountNature.Debit, IsActive = true };
        var payableGeneral = new GeneralAccount { Code = "210", Name = "Payable", AccountGroup = liabilities, Nature = AccountNature.Credit, IsActive = true };
        var cash = new SubsidiaryAccount
        {
            Code = "110201",
            Name = "Main Cash",
            SystemKey = "VAL_CASH",
            GeneralAccount = cashGeneral,
            Nature = AccountNature.Debit,
            IsActive = !deactivateCash
        };
        var payable = new SubsidiaryAccount
        {
            Code = "210201",
            Name = "Main Payable",
            SystemKey = "VAL_PAY",
            GeneralAccount = payableGeneral,
            Nature = AccountNature.Credit,
            IsActive = true,
            AllowsFloatingDetail = false
        };
        var floating = new FloatingDetailAccount
        {
            Code = "PRJ-VAL",
            Name = "Validation Project",
            Type = FloatingDetailAccountType.Project,
            IsActive = !deactivateFloatingDetail
        };

        context.AddRange(fiscalYear, generalJournal, cash, payable, floating);
        context.SubsidiaryAccountFloatingDetails.Add(new SubsidiaryAccountFloatingDetail
        {
            SubsidiaryAccount = cash,
            FloatingDetailAccount = floating
        });
        await context.SaveChangesAsync();

        if (existingCashBalance > 0m)
        {
            context.VoucherHeaders.Add(new VoucherHeader
            {
                DocumentNumber = "VAL-OPEN-01",
                VoucherDate = new DateTime(2036, 1, 5),
                Status = VoucherStatus.Permanent,
                PostingStatus = PostingStatus.Posted,
                FiscalYearId = fiscalYear.Id,
                JournalTypeId = generalJournal.Id,
                TotalDebits = existingCashBalance,
                TotalCredits = existingCashBalance,
                Lines =
                [
                    new VoucherLine
                    {
                        SubsidiaryAccountId = cash.Id,
                        FloatingDetailAccountId = floating.Id,
                        DebitAmount = existingCashBalance,
                        CreditAmount = 0m,
                        ExchangeRate = 1m,
                        DisplayOrder = 1
                    },
                    new VoucherLine
                    {
                        SubsidiaryAccountId = payable.Id,
                        DebitAmount = 0m,
                        CreditAmount = existingCashBalance,
                        ExchangeRate = 1m,
                        DisplayOrder = 2
                    }
                ]
            });
            await context.SaveChangesAsync();
        }

        return new ValidationSeed(fiscalYear.Id, generalJournal.Id, cash.Id, payable.Id, floating.Id);
    }

    private sealed record ValidationSeed(int FiscalYearId, int JournalTypeId, int CashAccountId, int PayableAccountId, Guid FloatingDetailId);
}
