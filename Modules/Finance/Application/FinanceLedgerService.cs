using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Models;
using OfficeAutomation.Modules.Finance.Domain;
using OfficeAutomation.Modules.Finance.Infrastructure.Persistence;

namespace OfficeAutomation.Modules.Finance.Application;

public sealed class FinanceLedgerService
{
    private readonly FinanceDbContext _context;
    private readonly VoucherValidationGuard _voucherValidationGuard;
    private readonly PeriodClosingService _periodClosingService;
    private static readonly string[] PermanentGroupCodes = ["1", "2"];

    public FinanceLedgerService(FinanceDbContext context)
    {
        _context = context;
        _voucherValidationGuard = new VoucherValidationGuard(context);
        _periodClosingService = new PeriodClosingService(context);
    }

    public async Task<VoucherHeader> ChangeVoucherStatusAsync(
        int voucherId,
        VoucherStatus targetStatus,
        CancellationToken cancellationToken = default)
    {
        var voucher = await _context.VoucherHeaders
            .Include(item => item.Lines)
            .FirstOrDefaultAsync(item => item.Id == voucherId, cancellationToken)
            ?? throw new InvalidOperationException($"Voucher '{voucherId}' was not found.");

        if (targetStatus == VoucherStatus.Reviewed && !voucher.IsBalanced())
        {
            throw new InvalidOperationException("Unbalanced vouchers cannot move to Reviewed status.");
        }

        if (targetStatus != VoucherStatus.Draft)
        {
            await _voucherValidationGuard.ValidateAsync(voucher, cancellationToken);
        }

        voucher.ChangeStatus(targetStatus);
        await _context.SaveChangesAsync(cancellationToken);
        return voucher;
    }

    public async Task<VoucherHeader> CreateManualVoucherAsync(SimpleVoucherCreateVM model, CancellationToken cancellationToken = default)
    {
        var validLines = model.Lines
            .Where(item => item.SubsidiaryAccountId > 0 && (item.DebitAmount > 0 || item.CreditAmount > 0 || item.ForeignAmount.HasValue))
            .ToList();

        if (validLines.Count < 2)
        {
            throw new InvalidOperationException("حداقل دو خط معتبر برای سند لازم است.");
        }

        var fiscalYear = await _context.FiscalYears
            .Where(item => item.StartDate <= model.VoucherDate.Date && item.EndDate >= model.VoucherDate.Date && !item.IsClosed)
            .OrderByDescending(item => item.StartDate)
            .FirstOrDefaultAsync(cancellationToken);
        if (fiscalYear == null)
        {
            throw new InvalidOperationException("سال مالی باز برای تاریخ سند یافت نشد.");
        }

        var journalType = await _context.JournalTypes
            .FirstOrDefaultAsync(item => item.Id == model.JournalTypeId && item.IsActive, cancellationToken)
            ?? await ResolveJournalTypeAsync(JournalTypeCodes.General, cancellationToken);

        var floatingPairs = validLines
            .Where(item => item.FloatingDetailAccountId.HasValue)
            .Select(item => new { item.SubsidiaryAccountId, FloatingDetailAccountId = item.FloatingDetailAccountId!.Value })
            .Distinct()
            .ToList();

        if (floatingPairs.Count > 0)
        {
            var allowedPairs = await _context.SubsidiaryAccountFloatingDetails
                .AsNoTracking()
                .Where(item => floatingPairs.Select(pair => pair.SubsidiaryAccountId).Contains(item.SubsidiaryAccountId))
                .Select(item => new { item.SubsidiaryAccountId, item.FloatingDetailAccountId })
                .ToListAsync(cancellationToken);

            var invalidPair = floatingPairs.FirstOrDefault(pair =>
                !allowedPairs.Any(allowed =>
                    allowed.SubsidiaryAccountId == pair.SubsidiaryAccountId &&
                    allowed.FloatingDetailAccountId == pair.FloatingDetailAccountId));

            if (invalidPair != null)
            {
                throw new InvalidOperationException("تفصیلی شناور انتخاب‌شده برای حساب معین مجاز نیست.");
            }
        }

        var voucher = new VoucherHeader
        {
            DocumentNumber = $"MANUAL-{model.VoucherDate:yyyyMMdd}-{Guid.NewGuid():N}"[..32],
            VoucherDate = model.VoucherDate.Date,
            Description = model.Description.Trim(),
            FiscalYearId = fiscalYear.Id,
            JournalTypeId = journalType.Id,
            Status = VoucherStatus.Draft,
            PostingStatus = PostingStatus.Draft,
            Lines = validLines.Select((line, index) => new VoucherLine
            {
                SubsidiaryAccountId = line.SubsidiaryAccountId,
                FloatingDetailAccountId = line.FloatingDetailAccountId,
                CurrencyId = line.CurrencyId,
                ExchangeRate = line.CurrencyId.HasValue ? line.ExchangeRate : 1m,
                ForeignAmount = line.CurrencyId.HasValue ? line.ForeignAmount : null,
                DebitAmount = line.DebitAmount,
                CreditAmount = line.CreditAmount,
                Narration = line.Narration,
                DisplayOrder = index + 1
            }).ToList()
        };

        foreach (var line in voucher.Lines.Where(item => item.CurrencyId.HasValue && item.ForeignAmount.HasValue))
        {
            var foreignAmount = line.ForeignAmount.GetValueOrDefault();
            var baseAmount = Math.Round(Math.Abs(foreignAmount) * line.ExchangeRate, 2, MidpointRounding.AwayFromZero);
            if (line.CreditAmount > 0 && line.DebitAmount == 0)
            {
                line.CreditAmount = baseAmount;
            }
            else
            {
                line.DebitAmount = baseAmount;
                line.CreditAmount = 0;
            }
        }

        voucher.TotalDebits = voucher.Lines.Sum(item => item.DebitAmount);
        voucher.TotalCredits = voucher.Lines.Sum(item => item.CreditAmount);
        if (voucher.TotalDebits != voucher.TotalCredits)
        {
            throw new InvalidOperationException("سند حسابداری تراز نیست.");
        }

        await _voucherValidationGuard.ValidateAsync(voucher, cancellationToken);

        _context.VoucherHeaders.Add(voucher);
        await _context.SaveChangesAsync(cancellationToken);
        return voucher;
    }

    public async Task<VoucherHeader> PostInvoiceAsync(Invoice invoice, CancellationToken cancellationToken = default)
    {
        if (invoice.Id <= 0)
        {
            throw new InvalidOperationException("Invoice must be persisted before posting an accounting voucher.");
        }

        var fiscalYear = await ResolveFiscalYearAsync(invoice.InvoiceDate, cancellationToken);
        if (fiscalYear.IsClosed)
        {
            throw new InvalidOperationException($"Fiscal year '{fiscalYear.YearName}' is closed.");
        }

        await EnsureLedgerDefaultsAsync(cancellationToken);

        var documentNumber = BuildInvoiceDocumentNumber(invoice);
        var existingVoucher = await _context.VoucherHeaders
            .Include(item => item.Lines)
            .FirstOrDefaultAsync(item => item.DocumentNumber == documentNumber, cancellationToken);

        if (existingVoucher?.PostingStatus == PostingStatus.Posted)
        {
            return existingVoucher;
        }

        var journalType = await ResolveJournalTypeAsync(
            string.Equals(invoice.InvoiceType, "Purchase", StringComparison.OrdinalIgnoreCase)
                ? JournalTypeCodes.Purchase
                : JournalTypeCodes.Sales,
            cancellationToken);

        var voucher = existingVoucher ?? new VoucherHeader
        {
            DocumentNumber = documentNumber
        };

        voucher.VoucherDate = invoice.InvoiceDate.Date;
        voucher.Description = BuildDescription(invoice);
        voucher.Status = VoucherStatus.Permanent;
        voucher.PostingStatus = PostingStatus.Posted;
        voucher.FiscalYearId = fiscalYear.Id;
        voucher.JournalTypeId = journalType.Id;

        if (existingVoucher != null && existingVoucher.Lines.Count > 0)
        {
            _context.VoucherLines.RemoveRange(existingVoucher.Lines);
            existingVoucher.Lines.Clear();
        }

        voucher.Lines = await BuildLinesAsync(invoice, cancellationToken);
        NormalizeLines(voucher.Lines);
        SetTotalsAndValidate(voucher);

        if (existingVoucher == null)
        {
            _context.VoucherHeaders.Add(voucher);
        }

        await _context.SaveChangesAsync(cancellationToken);
        return voucher;
    }

    public async Task<VoucherHeader> ReverseVoucherAsync(
        int voucherHeaderId,
        string? reason = null,
        DateTime? reversalDate = null,
        CancellationToken cancellationToken = default)
    {
        var original = await _context.VoucherHeaders
            .Include(item => item.Lines)
            .FirstOrDefaultAsync(item => item.Id == voucherHeaderId, cancellationToken);

        if (original == null)
        {
            throw new InvalidOperationException($"Voucher {voucherHeaderId} was not found.");
        }

        if (original.PostingStatus != PostingStatus.Posted)
        {
            throw new InvalidOperationException("Only posted vouchers can be reversed.");
        }

        if (await _context.VoucherHeaders.AnyAsync(item => item.ReversalOfVoucherHeaderId == original.Id, cancellationToken))
        {
            throw new InvalidOperationException("Voucher has already been reversed.");
        }

        var date = (reversalDate ?? DateTime.Today).Date;
        var fiscalYear = await ResolveFiscalYearAsync(date, cancellationToken);
        if (fiscalYear.IsClosed)
        {
            throw new InvalidOperationException($"Fiscal year '{fiscalYear.YearName}' is closed.");
        }

        var reversal = new VoucherHeader
        {
            DocumentNumber = $"REV-{original.DocumentNumber}",
            VoucherDate = date,
            Description = string.IsNullOrWhiteSpace(reason)
                ? $"Reversal of voucher {original.VoucherNumber}"
                : $"Reversal of voucher {original.VoucherNumber}: {reason}",
            Status = VoucherStatus.Permanent,
            PostingStatus = PostingStatus.Posted,
            FiscalYearId = fiscalYear.Id,
            JournalTypeId = original.JournalTypeId,
            ReversalOfVoucherHeaderId = original.Id,
            Lines = original.Lines
                .OrderBy(item => item.DisplayOrder)
                .Select(item => new VoucherLine
                {
                    SubsidiaryAccountId = item.SubsidiaryAccountId,
                    DetailedAccountId = item.DetailedAccountId,
                    FloatingDetailAccountId = item.FloatingDetailAccountId,
                    CostCenterId = item.CostCenterId,
                    CurrencyId = item.CurrencyId,
                    CurrencyRate = item.CurrencyRate,
                    ForeignAmount = item.ForeignAmount.HasValue ? -item.ForeignAmount.Value : null,
                    DebitAmount = item.CreditAmount,
                    CreditAmount = item.DebitAmount,
                    Narration = $"Reversal: {item.Narration}",
                    DisplayOrder = item.DisplayOrder
                })
                .ToList()
        };

        SetTotalsAndValidate(reversal);
        _context.VoucherHeaders.Add(reversal);
        await _context.SaveChangesAsync(cancellationToken);
        return reversal;
    }

    public async Task<VoucherHeader> CloseTemporaryAccountsAsync(Guid fiscalPeriodId, CancellationToken cancellationToken = default)
    {
        await EnsureLedgerDefaultsAsync(cancellationToken);
        var retainedEarnings = await ResolveSubsidiaryAsync(FinanceAccountKeys.RetainedEarnings, cancellationToken);
        return await _periodClosingService.CloseTemporaryAccountsAsync(fiscalPeriodId, retainedEarnings.Id, cancellationToken);
    }

    public async Task<VoucherHeader> CarryForwardBalancesAsync(int currentFiscalYearId, int nextFiscalYearId, CancellationToken cancellationToken = default)
    {
        await EnsureLedgerDefaultsAsync(cancellationToken);

        var currentYear = await _context.FiscalYears
            .FirstOrDefaultAsync(item => item.Id == currentFiscalYearId, cancellationToken)
            ?? throw new InvalidOperationException($"Current fiscal year '{currentFiscalYearId}' was not found.");
        var nextYear = await _context.FiscalYears
            .FirstOrDefaultAsync(item => item.Id == nextFiscalYearId, cancellationToken)
            ?? throw new InvalidOperationException($"Next fiscal year '{nextFiscalYearId}' was not found.");

        var documentNumber = $"OPEN-{nextYear.YearName}";
        var existing = await _context.VoucherHeaders
            .Include(item => item.Lines)
            .FirstOrDefaultAsync(item => item.DocumentNumber == documentNumber, cancellationToken);
        if (existing != null)
        {
            return existing;
        }

        var balances = await GetAccountBalancesAsync(currentYear.StartDate, currentYear.EndDate, PermanentGroupCodes, cancellationToken);
        var journalType = await ResolveJournalTypeAsync(JournalTypeCodes.Opening, cancellationToken);
        var lines = new List<VoucherLine>();
        var displayOrder = 1;

        foreach (var balance in balances)
        {
            var netBalance = balance.Debits - balance.Credits;
            if (netBalance == 0)
            {
                continue;
            }

            lines.Add(CreateLine(
                balance.SubsidiaryAccountId,
                netBalance > 0 ? netBalance : 0,
                netBalance < 0 ? Math.Abs(netBalance) : 0,
                $"Carry forward balance for {balance.AccountCode}",
                displayOrder++));
        }

        var voucher = new VoucherHeader
        {
            DocumentNumber = documentNumber,
            VoucherDate = nextYear.StartDate.Date,
            Description = $"Opening balances carried forward from {currentYear.YearName}",
            Status = VoucherStatus.Permanent,
            PostingStatus = PostingStatus.Posted,
            FiscalYearId = nextYear.Id,
            JournalTypeId = journalType.Id,
            Lines = lines
        };

        SetTotalsAndValidate(voucher);
        _context.VoucherHeaders.Add(voucher);
        await _context.SaveChangesAsync(cancellationToken);
        return voucher;
    }

    public async Task<VoucherHeader> RevalueForeignCurrenciesAsync(
        Guid fiscalPeriodId,
        Guid targetCurrencyId,
        decimal closingRate,
        Guid exchangeGainLossAccountId,
        CancellationToken cancellationToken = default)
    {
        if (closingRate <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(closingRate), "Closing rate must be positive.");
        }

        await EnsureLedgerDefaultsAsync(cancellationToken);

        var period = await _context.FiscalPeriods
            .Include(item => item.FiscalYear)
            .FirstOrDefaultAsync(item => item.Id == fiscalPeriodId, cancellationToken)
            ?? throw new InvalidOperationException($"Fiscal period '{fiscalPeriodId}' was not found.");

        if (period.Status == FiscalPeriodStatus.HardLocked)
        {
            throw new InvalidOperationException($"Fiscal period '{period.Name}' is hard locked and cannot be revalued.");
        }

        var currency = await _context.Currencies
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == targetCurrencyId && item.IsActive, cancellationToken)
            ?? throw new InvalidOperationException($"Currency '{targetCurrencyId}' was not found.");

        var gainLossAccount = await _context.SubsidiaryAccounts
            .FirstOrDefaultAsync(item => item.SystemKey == exchangeGainLossAccountId.ToString() && item.IsActive, cancellationToken)
            ?? throw new InvalidOperationException($"Exchange gain/loss account '{exchangeGainLossAccountId}' is not configured.");

        var documentNumber = $"FXREV-{period.FiscalYear.YearName}-{period.PeriodNumber:00}-{currency.Code}";
        var existing = await _context.VoucherHeaders
            .Include(item => item.Lines)
            .FirstOrDefaultAsync(item => item.DocumentNumber == documentNumber, cancellationToken);
        if (existing != null)
        {
            return existing;
        }

        var balances = await GetForeignCurrencyBalancesAsync(period.EndDate, targetCurrencyId, cancellationToken);
        var lines = new List<VoucherLine>();
        var displayOrder = 1;

        foreach (var balance in balances)
        {
            if (balance.ForeignBalance == 0)
            {
                continue;
            }

            var targetBaseValue = Math.Round(balance.ForeignBalance * closingRate, 2, MidpointRounding.AwayFromZero);
            var difference = targetBaseValue - balance.BookBaseBalance;
            if (difference == 0)
            {
                continue;
            }

            lines.Add(CreateLine(
                balance.SubsidiaryAccountId,
                targetCurrencyId,
                closingRate,
                difference > 0 ? difference : 0,
                difference < 0 ? Math.Abs(difference) : 0,
                $"FX revaluation for {currency.Code} account {balance.AccountCode}",
                displayOrder++));

            lines.Add(CreateLine(
                gainLossAccount.Id,
                difference < 0 ? Math.Abs(difference) : 0,
                difference > 0 ? difference : 0,
                $"Unrealized FX gain/loss for {currency.Code} account {balance.AccountCode}",
                displayOrder++));
        }

        if (lines.Count == 0)
        {
            throw new InvalidOperationException($"No foreign currency balances require revaluation for {currency.Code}.");
        }

        var journalType = await ResolveJournalTypeAsync(JournalTypeCodes.Adjustment, cancellationToken);
        var voucher = new VoucherHeader
        {
            DocumentNumber = documentNumber,
            VoucherDate = period.EndDate.Date,
            Description = $"Period-end FX revaluation for {currency.Code} at {closingRate:N4}",
            Status = VoucherStatus.Permanent,
            PostingStatus = PostingStatus.Posted,
            FiscalYearId = period.FiscalYearId,
            JournalTypeId = journalType.Id,
            Lines = lines
        };

        SetTotalsAndValidate(voucher);
        _context.VoucherHeaders.Add(voucher);
        await _context.SaveChangesAsync(cancellationToken);
        return voucher;
    }

    private async Task<List<VoucherLine>> BuildLinesAsync(Invoice invoice, CancellationToken cancellationToken)
    {
        var lines = string.Equals(invoice.InvoiceType, "Purchase", StringComparison.OrdinalIgnoreCase)
            ? await BuildPurchaseLinesAsync(invoice, cancellationToken)
            : await BuildSalesLinesAsync(invoice, cancellationToken);

        return lines
            .Where(item => item.DebitAmount > 0 || item.CreditAmount > 0)
            .ToList();
    }

    private async Task<List<VoucherLine>> BuildSalesLinesAsync(Invoice invoice, CancellationToken cancellationToken)
    {
        var receivable = await ResolveSubsidiaryAsync(FinanceAccountKeys.AccountsReceivable, cancellationToken);
        var revenue = await ResolveSubsidiaryAsync(FinanceAccountKeys.SalesRevenue, cancellationToken);
        var vat = await ResolveSubsidiaryAsync(FinanceAccountKeys.VatPayable, cancellationToken);
        var customer = await ResolveFloatingDetailAsync(receivable, FloatingDetailAccountType.Person, invoice.PartyName, invoice.PartyName, cancellationToken);

        return
        [
            CreateLine(receivable, customer, invoice.GrandTotal, 0, $"Receivable for invoice {invoice.InvoiceNumber}"),
            CreateLine(revenue, null, 0, invoice.SubTotal, $"Revenue for invoice {invoice.InvoiceNumber}"),
            CreateLine(vat, null, 0, invoice.VatAmount, $"Output VAT for invoice {invoice.InvoiceNumber}")
        ];
    }

    private async Task<List<VoucherLine>> BuildPurchaseLinesAsync(Invoice invoice, CancellationToken cancellationToken)
    {
        var expense = await ResolveSubsidiaryAsync(FinanceAccountKeys.PurchaseExpense, cancellationToken);
        var vat = await ResolveSubsidiaryAsync(FinanceAccountKeys.VatReceivable, cancellationToken);
        var payable = await ResolveSubsidiaryAsync(FinanceAccountKeys.AccountsPayable, cancellationToken);
        var vendor = await ResolveFloatingDetailAsync(payable, FloatingDetailAccountType.Person, invoice.PartyName, invoice.PartyName, cancellationToken);

        return
        [
            CreateLine(expense, null, invoice.SubTotal, 0, $"Purchase expense for invoice {invoice.InvoiceNumber}"),
            CreateLine(vat, null, invoice.VatAmount, 0, $"Input VAT for invoice {invoice.InvoiceNumber}"),
            CreateLine(payable, vendor, 0, invoice.GrandTotal, $"Payable for invoice {invoice.InvoiceNumber}")
        ];
    }

    private static VoucherLine CreateLine(
        SubsidiaryAccount subsidiary,
        FloatingDetailAccount? floatingDetail,
        decimal debit,
        decimal credit,
        string narration)
    {
        return new VoucherLine
        {
            SubsidiaryAccountId = subsidiary.Id,
            FloatingDetailAccountId = floatingDetail?.Id,
            DebitAmount = debit,
            CreditAmount = credit,
            ExchangeRate = 1m,
            Narration = narration
        };
    }

    private static VoucherLine CreateLine(
        int subsidiaryAccountId,
        decimal debit,
        decimal credit,
        string narration,
        int displayOrder)
    {
        return new VoucherLine
        {
            SubsidiaryAccountId = subsidiaryAccountId,
            DebitAmount = debit,
            CreditAmount = credit,
            ExchangeRate = 1m,
            Narration = narration,
            DisplayOrder = displayOrder
        };
    }

    private static VoucherLine CreateLine(
        int subsidiaryAccountId,
        Guid currencyId,
        decimal exchangeRate,
        decimal debit,
        decimal credit,
        string narration,
        int displayOrder)
    {
        return new VoucherLine
        {
            SubsidiaryAccountId = subsidiaryAccountId,
            CurrencyId = currencyId,
            ExchangeRate = exchangeRate,
            DebitAmount = debit,
            CreditAmount = credit,
            Narration = narration,
            DisplayOrder = displayOrder
        };
    }

    private static void NormalizeLines(List<VoucherLine> lines)
    {
        for (var index = 0; index < lines.Count; index++)
        {
            lines[index].DisplayOrder = index + 1;
        }
    }

    private static void SetTotalsAndValidate(VoucherHeader voucher)
    {
        voucher.TotalDebits = voucher.Lines.Sum(item => item.DebitAmount);
        voucher.TotalCredits = voucher.Lines.Sum(item => item.CreditAmount);

        if (voucher.TotalDebits != voucher.TotalCredits)
        {
            throw new InvalidOperationException(
                $"Voucher is not balanced. Debits={voucher.TotalDebits:N2}, Credits={voucher.TotalCredits:N2}.");
        }
    }

    private async Task<SubsidiaryAccount> ResolveSubsidiaryAsync(string systemKey, CancellationToken cancellationToken)
    {
        var account = await _context.SubsidiaryAccounts
            .FirstOrDefaultAsync(item => item.SystemKey == systemKey && item.IsActive, cancellationToken);

        return account ?? throw new InvalidOperationException($"Finance account '{systemKey}' is not configured.");
    }

    private async Task<JournalType> ResolveJournalTypeAsync(string code, CancellationToken cancellationToken)
    {
        var journal = await _context.JournalTypes
            .FirstOrDefaultAsync(item => item.Code == code && item.IsActive, cancellationToken);

        return journal ?? throw new InvalidOperationException($"Journal type '{code}' is not configured.");
    }

    private async Task<FloatingDetailAccount> ResolveFloatingDetailAsync(
        SubsidiaryAccount subsidiary,
        FloatingDetailAccountType type,
        string externalReference,
        string displayName,
        CancellationToken cancellationToken)
    {
        var reference = string.IsNullOrWhiteSpace(externalReference) ? "UNKNOWN" : externalReference.Trim();
        var detail = await _context.FloatingDetailAccounts
            .FirstOrDefaultAsync(item =>
                item.Type == type &&
                item.Code == reference,
                cancellationToken);

        if (detail != null)
        {
            await EnsureFloatingDetailLinkAsync(subsidiary.Id, detail.Id, cancellationToken);
            return detail;
        }

        var next = await _context.FloatingDetailAccounts.CountAsync(item => item.Type == type, cancellationToken) + 1;
        detail = new FloatingDetailAccount
        {
            Code = string.IsNullOrWhiteSpace(reference) ? $"{subsidiary.Code}-{next:0000}" : reference,
            Name = string.IsNullOrWhiteSpace(displayName) ? reference : displayName.Trim(),
            Type = type
        };

        _context.FloatingDetailAccounts.Add(detail);
        await _context.SaveChangesAsync(cancellationToken);
        await EnsureFloatingDetailLinkAsync(subsidiary.Id, detail.Id, cancellationToken);
        return detail;
    }

    private async Task EnsureFloatingDetailLinkAsync(int subsidiaryAccountId, Guid floatingDetailAccountId, CancellationToken cancellationToken)
    {
        var exists = await _context.SubsidiaryAccountFloatingDetails
            .AnyAsync(item => item.SubsidiaryAccountId == subsidiaryAccountId && item.FloatingDetailAccountId == floatingDetailAccountId, cancellationToken);

        if (exists)
        {
            return;
        }

        _context.SubsidiaryAccountFloatingDetails.Add(new SubsidiaryAccountFloatingDetail
        {
            SubsidiaryAccountId = subsidiaryAccountId,
            FloatingDetailAccountId = floatingDetailAccountId
        });
        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureLedgerDefaultsAsync(CancellationToken cancellationToken)
    {
        await EnsureJournalTypesAsync(cancellationToken);

        if (await _context.SubsidiaryAccounts.AnyAsync(cancellationToken))
        {
            return;
        }

        var assets = new AccountGroup { Code = "1", Name = "Assets", Nature = AccountNature.Debit };
        var liabilities = new AccountGroup { Code = "2", Name = "Liabilities", Nature = AccountNature.Credit };
        var equity = new AccountGroup { Code = "3", Name = "Equity", Nature = AccountNature.Credit };
        var revenue = new AccountGroup { Code = "4", Name = "Revenue", Nature = AccountNature.Credit };
        var expense = new AccountGroup { Code = "5", Name = "Expenses", Nature = AccountNature.Debit };

        var receivables = new GeneralAccount { Code = "110", Name = "Receivables", AccountGroup = assets, Nature = AccountNature.Debit };
        var taxAssets = new GeneralAccount { Code = "120", Name = "Tax Assets", AccountGroup = assets, Nature = AccountNature.Debit };
        var payables = new GeneralAccount { Code = "210", Name = "Payables", AccountGroup = liabilities, Nature = AccountNature.Credit };
        var taxLiabilities = new GeneralAccount { Code = "220", Name = "Tax Liabilities", AccountGroup = liabilities, Nature = AccountNature.Credit };
        var retained = new GeneralAccount { Code = "310", Name = "Retained Earnings", AccountGroup = equity, Nature = AccountNature.Credit };
        var sales = new GeneralAccount { Code = "410", Name = "Sales", AccountGroup = revenue, Nature = AccountNature.Credit };
        var purchases = new GeneralAccount { Code = "510", Name = "Purchases", AccountGroup = expense, Nature = AccountNature.Debit };

        _context.SubsidiaryAccounts.AddRange(
            new SubsidiaryAccount { Code = "110101", Name = "Accounts Receivable", SystemKey = FinanceAccountKeys.AccountsReceivable, GeneralAccount = receivables, Nature = AccountNature.Debit },
            new SubsidiaryAccount { Code = "210101", Name = "Accounts Payable", SystemKey = FinanceAccountKeys.AccountsPayable, GeneralAccount = payables, Nature = AccountNature.Credit },
            new SubsidiaryAccount { Code = "410101", Name = "Sales Revenue", SystemKey = FinanceAccountKeys.SalesRevenue, GeneralAccount = sales, Nature = AccountNature.Credit, IsTemporary = true, AllowsFloatingDetail = false },
            new SubsidiaryAccount { Code = "510101", Name = "Purchase Expense", SystemKey = FinanceAccountKeys.PurchaseExpense, GeneralAccount = purchases, Nature = AccountNature.Debit, IsTemporary = true, AllowsFloatingDetail = false },
            new SubsidiaryAccount { Code = "220301", Name = "VAT Payable", SystemKey = FinanceAccountKeys.VatPayable, GeneralAccount = taxLiabilities, Nature = AccountNature.Credit, AllowsFloatingDetail = false },
            new SubsidiaryAccount { Code = "120301", Name = "VAT Receivable", SystemKey = FinanceAccountKeys.VatReceivable, GeneralAccount = taxAssets, Nature = AccountNature.Debit, AllowsFloatingDetail = false },
            new SubsidiaryAccount { Code = "310101", Name = "Retained Earnings", SystemKey = FinanceAccountKeys.RetainedEarnings, GeneralAccount = retained, Nature = AccountNature.NoControl, AllowsFloatingDetail = false });

        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureJournalTypesAsync(CancellationToken cancellationToken)
    {
        var existingCodes = await _context.JournalTypes
            .Select(item => item.Code)
            .ToListAsync(cancellationToken);

        var journals = new[]
        {
            (JournalTypeCodes.Sales, "Sales Journal"),
            (JournalTypeCodes.Purchase, "Purchase Journal"),
            (JournalTypeCodes.Treasury, "Treasury Journal"),
            (JournalTypeCodes.Payroll, "Payroll Journal"),
            (JournalTypeCodes.General, "General Journal"),
            (JournalTypeCodes.Closing, "Closing Journal"),
            (JournalTypeCodes.Opening, "Opening Journal"),
            (JournalTypeCodes.Adjustment, "Adjustment Journal")
        };

        foreach (var (code, name) in journals)
        {
            if (!existingCodes.Contains(code, StringComparer.OrdinalIgnoreCase))
            {
                _context.JournalTypes.Add(new JournalType { Code = code, Name = name });
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task<FiscalYear> ResolveFiscalYearAsync(DateTime voucherDate, CancellationToken cancellationToken)
    {
        var date = voucherDate.Date;
        var fiscalYear = await _context.FiscalYears
            .FirstOrDefaultAsync(item => item.StartDate <= date && item.EndDate >= date, cancellationToken);

        if (fiscalYear != null)
        {
            return fiscalYear;
        }

        fiscalYear = new FiscalYear
        {
            YearName = date.Year.ToString(),
            StartDate = new DateTime(date.Year, 1, 1),
            EndDate = new DateTime(date.Year, 12, 31),
            IsClosed = false
        };

        _context.FiscalYears.Add(fiscalYear);
        await _context.SaveChangesAsync(cancellationToken);
        return fiscalYear;
    }

    private static string BuildInvoiceDocumentNumber(Invoice invoice)
    {
        var documentType = string.Equals(invoice.InvoiceType, "Purchase", StringComparison.OrdinalIgnoreCase)
            ? "PUR"
            : "SAL";

        return $"{documentType}-INV-{invoice.Id}";
    }

    private static string BuildDescription(Invoice invoice)
    {
        var typeLabel = string.Equals(invoice.InvoiceType, "Purchase", StringComparison.OrdinalIgnoreCase)
            ? "Purchase invoice"
            : "Sales invoice";

        return $"{typeLabel} {invoice.InvoiceNumber} - {invoice.PartyName}";
    }

    private async Task<List<AccountBalance>> GetAccountBalancesAsync(
        DateTime startDate,
        DateTime endDate,
        IReadOnlyCollection<string> accountGroupCodes,
        CancellationToken cancellationToken)
    {
        var from = startDate.Date;
        var to = endDate.Date;

        return await _context.VoucherLines
            .AsNoTracking()
            .Where(line =>
                line.VoucherHeader.VoucherDate >= from &&
                line.VoucherHeader.VoucherDate <= to &&
                line.VoucherHeader.PostingStatus == PostingStatus.Posted &&
                accountGroupCodes.Contains(line.SubsidiaryAccount.GeneralAccount.AccountGroup.Code))
            .GroupBy(line => new
            {
                line.SubsidiaryAccountId,
                line.SubsidiaryAccount.Code
            })
            .Select(group => new AccountBalance(
                group.Key.SubsidiaryAccountId,
                group.Key.Code,
                group.Sum(line => line.DebitAmount),
                group.Sum(line => line.CreditAmount)))
            .ToListAsync(cancellationToken);
    }

    private async Task<List<ForeignCurrencyBalance>> GetForeignCurrencyBalancesAsync(
        DateTime endDate,
        Guid currencyId,
        CancellationToken cancellationToken)
    {
        var to = endDate.Date;

        return await _context.VoucherLines
            .AsNoTracking()
            .Where(line =>
                line.CurrencyId == currencyId &&
                line.ForeignAmount.HasValue &&
                line.VoucherHeader.VoucherDate <= to &&
                line.VoucherHeader.PostingStatus == PostingStatus.Posted)
            .GroupBy(line => new
            {
                line.SubsidiaryAccountId,
                line.SubsidiaryAccount.Code
            })
            .Select(group => new ForeignCurrencyBalance(
                group.Key.SubsidiaryAccountId,
                group.Key.Code,
                group.Sum(line => line.DebitAmount - line.CreditAmount),
                group.Sum(line => line.DebitAmount > 0 ? line.ForeignAmount!.Value : -line.ForeignAmount!.Value)))
            .ToListAsync(cancellationToken);
    }

    private sealed record AccountBalance(
        int SubsidiaryAccountId,
        string AccountCode,
        decimal Debits,
        decimal Credits);

    private sealed record ForeignCurrencyBalance(
        int SubsidiaryAccountId,
        string AccountCode,
        decimal BookBaseBalance,
        decimal ForeignBalance);
}
