using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Modules.Finance.Application;
using OfficeAutomation.Modules.Finance.Domain;
using OfficeAutomation.Modules.Finance.Infrastructure.Persistence;
using Xunit;

namespace OfficeAutomation.Tests;

public sealed class RecurringJournalTests
{
    [Fact]
    public async Task CreateTemplate_RejectsUnbalancedTemplate()
    {
        using var context = CreateFinanceContext(nameof(CreateTemplate_RejectsUnbalancedTemplate));
        var seed = await SeedAsync(context, year: 2050);
        var service = new RecurringJournalService(context);

        var request = new TemplateCreateRequest
        {
            Name = "Unbalanced",
            JournalTypeId = seed.JournalTypeId,
            Lines =
            [
                new TemplateLineRequest { SubsidiaryAccountId = seed.DebitAccountId, DebitAmount = 100m, CreditAmount = 0m },
                new TemplateLineRequest { SubsidiaryAccountId = seed.CreditAccountId, DebitAmount = 0m, CreditAmount = 90m }
            ]
        };

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateTemplateAsync(request));
        Assert.Contains("balanced", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunTemplateAsync_GeneratesBalancedDraftVoucher()
    {
        using var context = CreateFinanceContext(nameof(RunTemplateAsync_GeneratesBalancedDraftVoucher));
        var seed = await SeedAsync(context, year: 2051);
        var service = new RecurringJournalService(context);

        var template = await service.CreateTemplateAsync(new TemplateCreateRequest
        {
            Name = "Monthly Rent",
            JournalTypeId = seed.JournalTypeId,
            IsRecurring = true,
            RecurrenceEveryMonths = 1,
            Lines =
            [
                new TemplateLineRequest { SubsidiaryAccountId = seed.DebitAccountId, DebitAmount = 500m, CreditAmount = 0m, Narration = "Rent expense" },
                new TemplateLineRequest { SubsidiaryAccountId = seed.CreditAccountId, DebitAmount = 0m, CreditAmount = 500m, Narration = "Payable" }
            ]
        }, default);

        var voucher = await service.RunTemplateAsync(template.Id, new DateTime(2051, 2, 1));

        Assert.Equal(VoucherStatus.Draft, voucher.Status);
        Assert.Equal(PostingStatus.Draft, voucher.PostingStatus);
        Assert.True(voucher.IsBalanced());
        Assert.Equal(500m, voucher.TotalDebits);
        Assert.Equal(500m, voucher.TotalCredits);
        Assert.Equal(2, voucher.Lines.Count);

        // Recurring template advanced its next run date.
        var updated = await context.JournalTemplates.AsNoTracking().SingleAsync(item => item.Id == template.Id);
        Assert.Equal(new DateTime(2051, 3, 1).Date, updated.NextRunDate?.Date);
    }

    [Fact]
    public async Task RunAndPostAsync_GeneratesAndPostsBalancedVoucher()
    {
        using var context = CreateFinanceContext(nameof(RunAndPostAsync_GeneratesAndPostsBalancedVoucher));
        var seed = await SeedAsync(context, year: 2052);
        var service = new RecurringJournalService(context);

        var template = await service.CreateTemplateAsync(new TemplateCreateRequest
        {
            Name = "Depreciation",
            JournalTypeId = seed.JournalTypeId,
            Lines =
            [
                new TemplateLineRequest { SubsidiaryAccountId = seed.DebitAccountId, DebitAmount = 120m, CreditAmount = 0m },
                new TemplateLineRequest { SubsidiaryAccountId = seed.CreditAccountId, DebitAmount = 0m, CreditAmount = 120m }
            ]
        }, default);

        var posted = await service.RunAndPostAsync(template.Id, new DateTime(2052, 1, 10));

        var persisted = await context.VoucherHeaders.AsNoTracking().SingleAsync(item => item.Id == posted.Id);
        Assert.Equal(VoucherStatus.Permanent, persisted.Status);
        Assert.Equal(PostingStatus.Posted, persisted.PostingStatus);
        Assert.Equal(120m, persisted.TotalDebits);
        Assert.Equal(120m, persisted.TotalCredits);
    }

    private static FinanceDbContext CreateFinanceContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<FinanceDbContext>()
            .UseInMemoryDatabase(databaseName)
            .EnableSensitiveDataLogging()
            .Options;

        return new FinanceDbContext(options);
    }

    private static async Task<TemplateSeed> SeedAsync(FinanceDbContext context, int year)
    {
        var fiscalYear = new FiscalYear
        {
            YearName = year.ToString(),
            StartDate = new DateTime(year, 1, 1),
            EndDate = new DateTime(year, 12, 31)
        };
        var generalJournal = new JournalType
        {
            Code = JournalTypeCodes.General,
            Name = "General",
            IsActive = true
        };
        var assets = new AccountGroup { Code = "1", Name = "Assets", Nature = AccountNature.Debit };
        var liabilities = new AccountGroup { Code = "2", Name = "Liabilities", Nature = AccountNature.Credit };
        var debitGeneral = new GeneralAccount { Code = "110", Name = "Assets", AccountGroup = assets, Nature = AccountNature.Debit };
        var creditGeneral = new GeneralAccount { Code = "210", Name = "Liabilities", AccountGroup = liabilities, Nature = AccountNature.Credit };
        var debitAccount = new SubsidiaryAccount
        {
            Code = "110701",
            Name = "Cash",
            SystemKey = $"TPL_CASH_{year}",
            GeneralAccount = debitGeneral,
            Nature = AccountNature.Debit,
            IsActive = true
        };
        var creditAccount = new SubsidiaryAccount
        {
            Code = "210701",
            Name = "Payable",
            SystemKey = $"TPL_PAY_{year}",
            GeneralAccount = creditGeneral,
            Nature = AccountNature.Credit,
            IsActive = true,
            AllowsFloatingDetail = false
        };

        context.AddRange(fiscalYear, generalJournal, debitAccount, creditAccount);
        await context.SaveChangesAsync();

        return new TemplateSeed(fiscalYear.Id, generalJournal.Id, debitAccount.Id, creditAccount.Id);
    }

    private sealed record TemplateSeed(int FiscalYearId, int JournalTypeId, int DebitAccountId, int CreditAccountId);
}