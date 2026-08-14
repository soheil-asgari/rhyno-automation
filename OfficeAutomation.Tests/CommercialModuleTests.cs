using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Models;
using OfficeAutomation.Modules.Commercial.Application;
using OfficeAutomation.Modules.Commercial.Domain;
using OfficeAutomation.Modules.Finance.Application;
using OfficeAutomation.Modules.Finance.Domain;
using OfficeAutomation.Modules.Finance.Infrastructure.Persistence;
using Xunit;

namespace OfficeAutomation.Tests;

public sealed class CommercialModuleTests
{
    // ── Payment status derived from actual settlements ───────────────────────
    [Fact]
    public void PaymentStatus_IsUnpaid_Partial_Full_FromSettlements()
    {
        Assert.Equal(CommercialPaymentStatus.Unpaid, CommercialPaymentStatus.Resolve(1000m, 0m, 0m, false));
        Assert.Equal(CommercialPaymentStatus.PartiallyPaid, CommercialPaymentStatus.Resolve(1000m, 400m, 0m, false));
        Assert.Equal(CommercialPaymentStatus.FullyPaid, CommercialPaymentStatus.Resolve(1000m, 1000m, 0m, false));
        Assert.Equal(CommercialPaymentStatus.Overpaid, CommercialPaymentStatus.Resolve(1000m, 1200m, 0m, false));
        Assert.Equal(CommercialPaymentStatus.Cancelled, CommercialPaymentStatus.Resolve(1000m, 0m, 0m, true));
    }

    // ── Return reduces invoice balance and posts a balanced reversal voucher ──
    [Fact]
    public async Task CreateReturn_UpdateBalance_AndFinalizePostsBalancedVoucher()
    {
        using var context = CreateFinanceContext(nameof(CreateReturn_UpdateBalance_AndFinalizePostsBalancedVoucher));
        await EnsureLedgerDefaultsAsync(context);
        var ledger = new FinanceLedgerService(context);

        // Create + post a sales invoice.
        var invoice = new Invoice
        {
            InvoiceNumber = "SAL-00001",
            InvoiceType = "Sale",
            DateShamsi = "1404/01/01",
            PartyName = "مشتری تست",
            SubTotal = 2000m,
            VatAmount = 200m,
            GrandTotal = 2200m,
            Amount = 2200m,
            VendorName = "مشتری تست",
            InvoiceDate = new DateTime(2025, 3, 21),
            WorkflowStatus = WorkflowStatus.Draft,
            CreatedAt = DateTime.Now
        };
        context.Invoices.Add(invoice);
        await context.SaveChangesAsync();
        await ledger.PostInvoiceAsync(invoice);

        // Create a sales return for part of the balance.
        var build = new Func<int, string, DateTime, IEnumerable<InvoiceReturnItem>, InvoiceReturn>((invoiceId, reason, date, items) =>
        {
            var entity = new InvoiceReturn
            {
                ReturnType = "Sale",
                ReturnNumber = "SRET-00001",
                InvoiceId = invoiceId,
                Reason = reason,
                ReturnDate = date,
                DateShamsi = "1404/02/01",
                Status = WorkflowStatus.Draft,
                SubTotal = 1000m,
                VatAmount = 100m,
                GrandTotal = 1100m,
                Items = items.ToList()
            };
            context.InvoiceReturns.Add(entity);
            return entity;
        });

        var returnEntity = build(invoice.Id, "برگشت کالا", new DateTime(2025, 4, 1), new[]
        {
            new InvoiceReturnItem { ItemName = "کالا ۱", Quantity = 1, UnitPrice = 1000m, LineSubTotal = 1000m, LineVatAmount = 100m, LineGrandTotal = 1100m }
        });
        await context.SaveChangesAsync();

        // Outstanding before finalize = grand total - return (approved returns reduce).
        var service = new CommercialService(context, ledger);
        var detail = await service.BuildInvoiceDetailAsync(invoice.Id, CancellationToken.None);
        Assert.NotNull(detail);

        // Finalize posts a balanced reversal-style voucher.
        var voucher = await service.FinalizeReturnAsync(returnEntity.Id, CancellationToken.None);
        var persisted = await context.VoucherHeaders.Include(item => item.Lines)
            .FirstOrDefaultAsync(item => item.DocumentNumber == $"RET-SAL-{returnEntity.Id}", CancellationToken.None);
        Assert.NotNull(persisted);
        Assert.Equal(PostingStatus.Posted, persisted.PostingStatus);
        Assert.Equal(1100m, persisted.TotalDebits);
        Assert.Equal(1100m, persisted.TotalCredits);

        var persistedReturn = await context.InvoiceReturns.FirstOrDefaultAsync(item => item.Id == returnEntity.Id, CancellationToken.None);
        Assert.Equal(WorkflowStatus.Approved, persistedReturn!.Status);
        Assert.NotNull(persistedReturn.VoucherDocumentNumber);
    }

    // ── Settlement posts a balanced treasury voucher and marks invoice paid ──
    [Fact]
    public async Task RegisterSettlement_PostsTreasuryVoucher_AndPaymentStatusBecomesFullyPaid()
    {
        using var context = CreateFinanceContext(nameof(RegisterSettlement_PostsTreasuryVoucher_AndPaymentStatusBecomesFullyPaid));
        await EnsureLedgerDefaultsAsync(context);
        var ledger = new FinanceLedgerService(context);

        var invoice = new Invoice
        {
            InvoiceNumber = "SAL-00002",
            InvoiceType = "Sale",
            DateShamsi = "1404/01/01",
            PartyName = "مشتری نقدی",
            SubTotal = 1000m,
            VatAmount = 100m,
            GrandTotal = 1100m,
            Amount = 1100m,
            VendorName = "مشتری نقدی",
            InvoiceDate = new DateTime(2025, 3, 21),
            WorkflowStatus = WorkflowStatus.Draft,
            CreatedAt = DateTime.Now
        };
        context.Invoices.Add(invoice);
        await context.SaveChangesAsync();

        var service = new CommercialService(context, ledger);
        var settlement = await service.RegisterSettlementAsync(
            invoice.Id,
            1100m,
            "R-001",
            "Cash",
            "1404/01/15",
            new DateTime(2025, 4, 4),
            null,
            null,
            postNow: true,
            CancellationToken.None);

        var voucher = await context.VoucherHeaders
            .FirstOrDefaultAsync(item => item.DocumentNumber == $"REC-{settlement.Id}", CancellationToken.None);
        Assert.NotNull(voucher);
        Assert.Equal(PostingStatus.Posted, voucher!.PostingStatus);
        Assert.Equal(1100m, voucher.TotalDebits);
        Assert.Equal(1100m, voucher.TotalCredits);

        // Payment status now fully paid (settlement counted).
        var detail = await service.BuildInvoiceDetailAsync(invoice.Id, CancellationToken.None);
        Assert.NotNull(detail);
        Assert.Equal(CommercialPaymentStatus.FullyPaid, detail!.PaymentStatus);
        Assert.Equal(1100m, detail.PaidAmount);
        Assert.Equal(0m, detail.OutstandingAmount);
    }

    // ── Quotation converts to invoice without duplicating master data ────────
    [Fact]
    public async Task Quotation_ConvertsToInvoice_AndMarksConverted()
    {
        using var context = CreateFinanceContext(nameof(Quotation_ConvertsToInvoice_AndMarksConverted));
        await EnsureLedgerDefaultsAsync(context);
        var ledger = new FinanceLedgerService(context);

        var quotation = new CommercialQuotation
        {
            QuotationType = "Sale",
            QuotationNumber = "SQUO-00001",
            PartyName = "مشتری پیش‌فاکتور",
            DateShamsi = "1404/01/01",
            QuotationDate = new DateTime(2025, 3, 21),
            SubTotal = 500m,
            VatAmount = 50m,
            GrandTotal = 550m,
            Status = CommercialQuotationStatus.Draft,
            CreatedAt = DateTime.Now,
            Lines = new List<CommercialQuotationLine>
            {
                new() { ItemName = "خدمت ۱", Quantity = 1, UnitPrice = 500m, LineSubTotal = 500m, LineVatAmount = 50m, LineGrandTotal = 550m }
            }
        };
        context.CommercialQuotations.Add(quotation);
        await context.SaveChangesAsync();

        var service = new CommercialService(context, ledger);
        var (invoice, _) = await service.ConvertQuotationToInvoiceAsync(quotation.Id, "Sale", "1404/03/01", new DateTime(2025, 5, 22), null, CancellationToken.None);

        Assert.NotNull(invoice);
        Assert.True(invoice.Id > 0);
        Assert.Equal("مشتری پیش‌فاکتور", invoice.PartyName);
        Assert.Equal(550m, invoice.GrandTotal);

        var updated = await context.CommercialQuotations.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == quotation.Id, CancellationToken.None);
        Assert.Equal(CommercialQuotationStatus.Converted, updated!.Status);
        Assert.Equal(invoice.Id, updated.ConvertedInvoiceId);

        // No duplicate master party data created.
        Assert.Empty(await context.CommercialParties.ToListAsync(CancellationToken.None));
    }

    // ── Party CRUD ───────────────────────────────────────────────────────────
    [Fact]
    public async Task SaveParty_CreatesAndUpdatesUnifiedParty()
    {
        using var context = CreateFinanceContext(nameof(SaveParty_CreatesAndUpdatesUnifiedParty));
        await EnsureLedgerDefaultsAsync(context);
        var ledger = new FinanceLedgerService(context);
        var service = new CommercialService(context, ledger);

        var (created, isNew) = await service.SavePartyAsync(new PartyUpsertVM
        {
            Name = "شرکت نمونه",
            PartyType = CommercialPartyType.Both,
            EconomicCode = "123456"
        }, "user-1", CancellationToken.None);

        Assert.True(isNew);
        Assert.True(created.Id > 0);

        var (updated, isNewAgain) = await service.SavePartyAsync(new PartyUpsertVM
        {
            Id = created.Id,
            Name = "شرکت نمونه ویرایش",
            PartyType = CommercialPartyType.Supplier,
            EconomicCode = "123456"
        }, "user-1", CancellationToken.None);

        Assert.False(isNewAgain);
        Assert.Equal("شرکت نمونه ویرایش", updated.Name);
        Assert.Equal(CommercialPartyType.Supplier, updated.PartyType);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────
    private static FinanceDbContext CreateFinanceContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<FinanceDbContext>()
            .UseInMemoryDatabase(databaseName)
            .EnableSensitiveDataLogging()
            .Options;

        return new FinanceDbContext(options);
    }

    private static async Task EnsureLedgerDefaultsAsync(FinanceDbContext context)
    {
        var service = new FinanceLedgerService(context);
        // Force ledger default seeding by resolving a voucher posting path.
        var invoice = new Invoice
        {
            InvoiceNumber = "SEED",
            InvoiceType = "Sale",
            PartyName = "SEED",
            SubTotal = 1m,
            VatAmount = 0m,
            GrandTotal = 1m,
            Amount = 1m,
            VendorName = "SEED",
            InvoiceDate = DateTime.Today,
            WorkflowStatus = WorkflowStatus.Draft
        };
        context.Invoices.Add(invoice);
        await context.SaveChangesAsync();
        await service.PostInvoiceAsync(invoice);
    }
}