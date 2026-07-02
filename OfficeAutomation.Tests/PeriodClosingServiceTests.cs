using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Models;
using OfficeAutomation.Modules.Finance.Application;
using OfficeAutomation.Modules.Finance.Domain;
using OfficeAutomation.Modules.Finance.Infrastructure.Persistence;
using Xunit;

namespace OfficeAutomation.Tests;

public sealed class PeriodClosingServiceTests
{
    [Fact]
    public async Task CloseTemporaryAccounts_CreatesBalancedReviewedVoucher()
    {
        using var context = CreateFinanceContext(nameof(CloseTemporaryAccounts_CreatesBalancedReviewedVoucher));
        var period = await SeedOpenPeriodAsync(context, 2037);
        var ledger = new FinanceLedgerService(context);
        await PostTemporaryActivityAsync(ledger, 2037);

        var destinationAccountId = await context.SubsidiaryAccounts
            .Where(item => item.SystemKey == FinanceAccountKeys.RetainedEarnings)
            .Select(item => item.Id)
            .SingleAsync();

        var service = new PeriodClosingService(context);
        var voucher = await service.CloseTemporaryAccountsAsync(period.Id, destinationAccountId);

        var persisted = await context.VoucherHeaders
            .Include(item => item.JournalType)
            .Include(item => item.Lines)
            .SingleAsync(item => item.Id == voucher.Id);

        Assert.Equal(VoucherStatus.Reviewed, persisted.Status);
        Assert.Equal(JournalTypeCodes.Closing, persisted.JournalType.Code);
        Assert.Equal(persisted.TotalDebits, persisted.TotalCredits);
        Assert.Equal(100m, persisted.TotalDebits);
    }

    [Fact]
    public async Task CloseTemporaryAccounts_ZeroesTemporaryBalancesAfterClosingVoucher()
    {
        using var context = CreateFinanceContext(nameof(CloseTemporaryAccounts_ZeroesTemporaryBalancesAfterClosingVoucher));
        var period = await SeedOpenPeriodAsync(context, 2038);
        var ledger = new FinanceLedgerService(context);
        await PostTemporaryActivityAsync(ledger, 2038);

        var destinationAccountId = await context.SubsidiaryAccounts
            .Where(item => item.SystemKey == FinanceAccountKeys.RetainedEarnings)
            .Select(item => item.Id)
            .SingleAsync();

        var service = new PeriodClosingService(context);
        await service.CloseTemporaryAccountsAsync(period.Id, destinationAccountId);

        var balances = await context.VoucherLines
            .AsNoTracking()
            .Where(line =>
                line.VoucherHeader.FiscalYearId == period.FiscalYearId &&
                line.VoucherHeader.VoucherDate >= period.StartDate &&
                line.VoucherHeader.VoucherDate <= period.EndDate &&
                line.VoucherHeader.TotalDebits == line.VoucherHeader.TotalCredits &&
                line.SubsidiaryAccount.IsTemporary)
            .GroupBy(line => line.SubsidiaryAccount.SystemKey)
            .Select(group => new
            {
                SystemKey = group.Key,
                Net = group.Sum(item => item.DebitAmount) - group.Sum(item => item.CreditAmount)
            })
            .ToListAsync();

        Assert.All(balances, item => Assert.Equal(0m, item.Net));
    }

    [Fact]
    public async Task CloseTemporaryAccounts_DoesNotConflictWithNatureGuardWhenDestinationIsNoControl()
    {
        using var context = CreateFinanceContext(nameof(CloseTemporaryAccounts_DoesNotConflictWithNatureGuardWhenDestinationIsNoControl));
        var period = await SeedOpenPeriodAsync(context, 2039);
        var ledger = new FinanceLedgerService(context);
        await PostTemporaryActivityAsync(ledger, 2039);

        var destinationAccountId = await context.SubsidiaryAccounts
            .Where(item => item.SystemKey == FinanceAccountKeys.RetainedEarnings)
            .Select(item => item.Id)
            .SingleAsync();

        var destinationNature = await context.SubsidiaryAccounts
            .Where(item => item.Id == destinationAccountId)
            .Select(item => item.Nature)
            .SingleAsync();
        Assert.Equal(AccountNature.NoControl, destinationNature);

        var service = new PeriodClosingService(context);
        var voucher = await service.CloseTemporaryAccountsAsync(period.Id, destinationAccountId);

        Assert.True(voucher.TotalDebits == voucher.TotalCredits);
        Assert.Contains(voucher.Lines, item => item.SubsidiaryAccountId == destinationAccountId);
    }

    private static async Task PostTemporaryActivityAsync(FinanceLedgerService ledger, int year)
    {
        await ledger.PostInvoiceAsync(new Invoice
        {
            Id = 501,
            InvoiceNumber = "CL-S-501",
            InvoiceType = "Sale",
            DateShamsi = "1406/10/11",
            PartyName = "Closing Customer",
            VendorName = "Closing Customer",
            SubTotal = 100m,
            VatAmount = 0m,
            GrandTotal = 100m,
            Amount = 100m,
            InvoiceDate = new DateTime(year, 1, 10)
        });

        await ledger.PostInvoiceAsync(new Invoice
        {
            Id = 502,
            InvoiceNumber = "CL-P-502",
            InvoiceType = "Purchase",
            DateShamsi = "1406/10/12",
            PartyName = "Closing Vendor",
            VendorName = "Closing Vendor",
            SubTotal = 30m,
            VatAmount = 0m,
            GrandTotal = 30m,
            Amount = 30m,
            InvoiceDate = new DateTime(year, 1, 11)
        });
    }

    private static FinanceDbContext CreateFinanceContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<FinanceDbContext>()
            .UseInMemoryDatabase(databaseName)
            .EnableSensitiveDataLogging()
            .Options;

        return new FinanceDbContext(options);
    }

    private static async Task<FiscalPeriod> SeedOpenPeriodAsync(FinanceDbContext context, int year)
    {
        var fiscalYear = new FiscalYear
        {
            YearName = year.ToString(),
            StartDate = new DateTime(year, 1, 1),
            EndDate = new DateTime(year, 12, 31)
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

        context.FiscalYears.Add(fiscalYear);
        context.FiscalPeriods.Add(period);
        await context.SaveChangesAsync();
        return period;
    }
}
