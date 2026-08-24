using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OfficeAutomation.Models;
using OfficeAutomation.Modules.Finance.Domain;
using OfficeAutomation.Modules.Finance.Infrastructure.Persistence;
using OfficeAutomation.Modules.Office.Infrastructure.Persistence;

namespace OfficeAutomation.Services.Contracts;

/// <summary>
/// پياده‌سازی سرويس حسابداری قراردادها با مبنای تعهدی (Accrual Basis).
/// سه مرحله:
/// 1. فعال‌سازی ← بدهکار دريافتنی / بستانکار درآمد معوق (کل مبلغ قرارداد)
/// 2. دريافت وجه ← بدهکار بانک/صندوق / بستانکار دريافتنی (کاهش مانده)
/// 3. شناسايی دوره‌ای ← بدهکار درآمد معوق / بستانکار درآمد (به نسبت زمان)
/// </summary>
public sealed class ContractAccountingService : IContractAccountingService
{
    private readonly FinanceDbContext _financeContext;
    private readonly OfficeDbContext _officeContext;
    private readonly NotificationService _notificationService;
    private readonly ILogger<ContractAccountingService> _logger;

    private const string SystemKeyReceivable      = "CONTRACT_RECEIVABLE";
    private const string SystemKeyDeferredRevenue = "CONTRACT_DEFERRED_REVENUE";
    private const string SystemKeyRevenue         = "CONTRACT_REVENUE";
    private const string SystemKeyCash            = "CONTRACT_CASH";

    private const string NotificationModule     = "Contracts";
    private const string NotificationEntityType = "Accounting";

    public ContractAccountingService(
        FinanceDbContext financeContext,
        OfficeDbContext officeContext,
        NotificationService notificationService,
        ILogger<ContractAccountingService> logger)
    {
        _financeContext = financeContext;
        _officeContext = officeContext;
        _notificationService = notificationService;
        _logger = logger;
    }

    // ═════════════════════════════════════════════════════════════════
    //  مرحله 1: ثبت سند افتتاحيه
    // ═════════════════════════════════════════════════════════════════
    public async Task RecordDeferredRevenueAsync(
        Contract contract,
        string currentUserId,
        CancellationToken ct = default)
    {
        if (contract.TotalAmount <= 0)
        {
            _logger.LogWarning("Contract {Id} has zero TotalAmount. Skipping deferred revenue booking.", contract.Id);
            return;
        }

        var fy = await ResolveFiscalYearAsync(contract.StartDate, contract.Id, ct);
        if (fy == null) return;

        var jt = await ResolveJournalTypeAsync(ct);
        if (jt == null) return;

        var ra = await ResolveSubsidiaryAsync(SystemKeyReceivable, "حساب دريافتنی قراردادها", ct);
        var da = await ResolveSubsidiaryAsync(SystemKeyDeferredRevenue, "درآمد معوق قراردادها", ct);

        var v = new VoucherHeader
        {
            SequenceNumber  = await NextSequenceAsync(fy.Id, ct),
            DocumentNumber  = "CNTR-" + contract.ContractNumber + "-DEFER",
            VoucherDate     = contract.StartDate,
            Description     = "ثبت درآمد معوق قرارداد " + contract.ContractNumber + " - " + contract.Title,
            Reference       = "ContractDeferred#" + contract.Id,
            Status          = VoucherStatus.Draft,
            PostingStatus   = PostingStatus.Draft,
            IsMemo          = false,
            IssuedBy        = currentUserId,
            IssuedAt        = DateTimeOffset.UtcNow,
            FiscalYearId    = fy.Id,
            JournalTypeId   = jt.Id,
        };

        v.Lines = new List<VoucherLine>
        {
            new() { SubsidiaryAccountId = ra.Id, DebitAmount = contract.TotalAmount, CreditAmount = 0m, Narration = "دريافتنی از " + contract.ClientName, DisplayOrder = 1 },
            new() { SubsidiaryAccountId = da.Id, DebitAmount = 0m, CreditAmount = contract.TotalAmount, Narration = "درآمد معوق قرارداد " + contract.ContractNumber, DisplayOrder = 2 },
        };

        SetTotalsAndValidate(v);
        _financeContext.VoucherHeaders.Add(v);
        await _financeContext.SaveChangesAsync(ct);

        _logger.LogInformation("Deferred revenue voucher {0} created for contract {1}. Amount={2}", v.Id, contract.Id, contract.TotalAmount);
    }

    // ═════════════════════════════════════════════════════════════════
    //  مرحله 2: ثبت سند دريافت وجه نقد
    // ═════════════════════════════════════════════════════════════════
    public async Task RecordCashReceiptAsync(
        Contract contract,
        ContractPayment payment,
        string currentUserId,
        CancellationToken ct = default)
    {
        var fy = await ResolveFiscalYearAsync(payment.PaymentDate, contract.Id, ct);
        if (fy == null) return;

        var jt = await ResolveJournalTypeAsync(ct);
        if (jt == null) return;

        var ca = await ResolveSubsidiaryAsync(SystemKeyCash, "وجه نقد / بانک قراردادها", ct);
        var ra = await ResolveSubsidiaryAsync(SystemKeyReceivable, "حساب دريافتنی قراردادها", ct);

        var v = new VoucherHeader
        {
            SequenceNumber  = await NextSequenceAsync(fy.Id, ct),
            DocumentNumber  = "CNTR-" + contract.ContractNumber + "-PAY-" + payment.Id,
            VoucherDate     = payment.PaymentDate,
            Description     = "دريافت وجه قرارداد " + contract.ContractNumber + " - " + contract.Title,
            Reference       = "ContractPayment#" + payment.Id,
            Status          = VoucherStatus.Draft,
            PostingStatus   = PostingStatus.Draft,
            IsMemo          = false,
            IssuedBy        = currentUserId,
            IssuedAt        = DateTimeOffset.UtcNow,
            FiscalYearId    = fy.Id,
            JournalTypeId   = jt.Id,
        };

        v.Lines = new List<VoucherLine>
        {
            new() { SubsidiaryAccountId = ca.Id, DebitAmount = payment.Amount, CreditAmount = 0m, Narration = "دريافت از " + contract.ClientName + " - " + (payment.Notes ?? ""), DisplayOrder = 1 },
            new() { SubsidiaryAccountId = ra.Id, DebitAmount = 0m, CreditAmount = payment.Amount, Narration = "کاهش مانده دريافتنی قرارداد " + contract.ContractNumber, DisplayOrder = 2 },
        };

        SetTotalsAndValidate(v);
        _financeContext.VoucherHeaders.Add(v);
        await _financeContext.SaveChangesAsync(ct);

        _logger.LogInformation("Cash receipt voucher {0} for payment {1} on contract {2}.", v.Id, payment.Id, contract.Id);
    }

    // ═════════════════════════════════════════════════════════════════
    //  برگشت سند دريافت وجه
    // ═════════════════════════════════════════════════════════════════
    public async Task RevertCashReceiptAsync(
        Contract contract,
        ContractPayment deletedPayment,
        string currentUserId,
        CancellationToken ct = default)
    {
        var reference = "ContractPayment#" + deletedPayment.Id;
        var original = await _financeContext.VoucherHeaders
            .Include(v => v.Lines)
            .Where(v => v.Reference == reference && v.ReversedByVoucherHeaderId == null)
            .FirstOrDefaultAsync(ct);

        if (original == null)
        {
            _logger.LogWarning("No original cash receipt voucher for payment {0}. Skipping reversal.", deletedPayment.Id);
            return;
        }

        var reversalDate = DateTime.UtcNow;
        var fy = await ResolveFiscalYearAsync(reversalDate, contract.Id, ct);
        if (fy == null) return;

        var jt = await ResolveJournalTypeAsync(ct);
        if (jt == null) return;

        var reversal = new VoucherHeader
        {
            SequenceNumber              = await NextSequenceAsync(fy.Id, ct),
            DocumentNumber              = "CNTR-" + contract.ContractNumber + "-REV-" + deletedPayment.Id,
            VoucherDate                 = reversalDate,
            Description                 = "برگشت دريافت وجه قرارداد " + contract.ContractNumber,
            Reference                   = "ReversalOf#" + deletedPayment.Id,
            Status                      = VoucherStatus.Draft,
            PostingStatus               = PostingStatus.Draft,
            IsMemo                      = false,
            IssuedBy                    = currentUserId,
            IssuedAt                    = DateTimeOffset.UtcNow,
            FiscalYearId                = fy.Id,
            JournalTypeId               = jt.Id,
            ReversalOfVoucherHeaderId   = original.Id,
        };

        reversal.Lines = original.Lines.Select(l => new VoucherLine
        {
            SubsidiaryAccountId = l.SubsidiaryAccountId,
            DebitAmount         = l.CreditAmount,
            CreditAmount        = l.DebitAmount,
            Narration           = "برگشت: " + l.Narration,
            DisplayOrder        = l.DisplayOrder,
        }).ToList();

        SetTotalsAndValidate(reversal);
        _financeContext.VoucherHeaders.Add(reversal);
        await _financeContext.SaveChangesAsync(ct);

        _logger.LogInformation("Cash receipt reversal voucher {0} for deleted payment {1}.", reversal.Id, deletedPayment.Id);
    }

    // ═════════════════════════════════════════════════════════════════
    //  مرحله 3: شناسايی درآمد معوق به عنوان درآمد قطعی
    // ═════════════════════════════════════════════════════════════════
    public async Task<decimal> RecognizeRevenueAsync(
        int contractId,
        DateTime asOfDate,
        string currentUserId,
        CancellationToken ct = default)
    {
        var contract = await _officeContext.Contracts.FindAsync(new object[] { contractId }, ct);
        if (contract == null) throw new KeyNotFoundException("Contract " + contractId + " not found.");
        if (contract.TotalAmount <= 0 || contract.EndDate == null) return 0m;
        if (contract.Status != ContractStatuses.Active) return 0m;

        var recognitionStart = contract.LastRevenueRecognitionDate ?? contract.StartDate;
        if (asOfDate <= recognitionStart) return 0m;

        var totalDays = (contract.EndDate.Value - contract.StartDate).TotalDays;
        if (totalDays <= 0) return 0m;

        var elapsedDays = (asOfDate - recognitionStart).TotalDays;
        var dailyRate = contract.TotalAmount / (decimal)totalDays;
        var recognizeAmount = Math.Round(dailyRate * (decimal)elapsedDays, 2);
        if (recognizeAmount <= 0) return 0m;

        var maxRecognizable = contract.TotalAmount - contract.RecognizedRevenue;
        if (recognizeAmount > maxRecognizable) recognizeAmount = maxRecognizable;
        if (recognizeAmount <= 0) return 0m;

        var fy = await ResolveFiscalYearAsync(asOfDate, contract.Id, ct);
        if (fy == null) return 0m;

        var jt = await ResolveJournalTypeAsync(ct);
        if (jt == null) return 0m;

        var da = await ResolveSubsidiaryAsync(SystemKeyDeferredRevenue, "درآمد معوق قراردادها", ct);
        var rv = await ResolveSubsidiaryAsync(SystemKeyRevenue, "درآمد قراردادها", ct);

        var v = new VoucherHeader
        {
            SequenceNumber  = await NextSequenceAsync(fy.Id, ct),
            DocumentNumber  = "CNTR-" + contract.ContractNumber + "-REV-" + asOfDate.ToString("yyyyMMdd"),
            VoucherDate     = asOfDate,
            Description     = "شناسايی درآمد قرارداد " + contract.ContractNumber + " تا " + asOfDate.ToString("yyyy-MM-dd"),
            Reference       = "RevenueRecognition#" + contract.Id + "#" + asOfDate.ToString("yyyyMMdd"),
            Status          = VoucherStatus.Draft,
            PostingStatus   = PostingStatus.Draft,
            IsMemo          = false,
            IssuedBy        = currentUserId,
            IssuedAt        = DateTimeOffset.UtcNow,
            FiscalYearId    = fy.Id,
            JournalTypeId   = jt.Id,
        };

        v.Lines = new List<VoucherLine>
        {
            new() { SubsidiaryAccountId = da.Id, DebitAmount = recognizeAmount, CreditAmount = 0m, Narration = "انتقال از درآمد معوق به درآمد قطعی", DisplayOrder = 1 },
            new() { SubsidiaryAccountId = rv.Id, DebitAmount = 0m, CreditAmount = recognizeAmount, Narration = "شناسايی درآمد قرارداد " + contract.ContractNumber, DisplayOrder = 2 },
        };

        SetTotalsAndValidate(v);
        _financeContext.VoucherHeaders.Add(v);

        contract.RecognizedRevenue += recognizeAmount;
        contract.LastRevenueRecognitionDate = asOfDate;

        await _financeContext.SaveChangesAsync(ct);
        await _officeContext.SaveChangesAsync(ct);

        _logger.LogInformation("Revenue recognition voucher {0} for contract {1}. Amount={2}, TotalRecognized={3}",
            v.Id, contract.Id, recognizeAmount, contract.RecognizedRevenue);
        return recognizeAmount;
    }

    // ═════════════════════════════════════════════════════════════════
    //  اجرای دسته‌ای شناسايی درآمد
    // ═════════════════════════════════════════════════════════════════
    public async Task<int> RecognizeAllActiveContractsAsync(
        DateTime asOfDate,
        string systemUserId,
        CancellationToken ct = default)
    {
        var active = await _officeContext.Contracts
            .Where(c => c.Status == ContractStatuses.Active
                && c.EndDate != null
                && c.TotalAmount > 0
                && (c.LastRevenueRecognitionDate == null || c.LastRevenueRecognitionDate < c.EndDate))
            .ToListAsync(ct);

        int count = 0;
        foreach (var c in active)
        {
            try
            {
                var r = await RecognizeRevenueAsync(c.Id, asOfDate, systemUserId, ct);
                if (r > 0) count++;
            }
            catch (Exception ex) { _logger.LogError(ex, "Revenue recognition failed for contract {0}.", c.Id); }
        }

        _logger.LogInformation("Revenue recognition batch: {0}/{1} contracts updated.", count, active.Count);
        return count;
    }

    // ═════════════════════════════════════════════════════════════════
    //  Private helpers
    // ═════════════════════════════════════════════════════════════════

    private static void SetTotalsAndValidate(VoucherHeader v)
    {
        v.TotalDebits = v.Lines.Sum(l => l.DebitAmount);
        v.TotalCredits = v.Lines.Sum(l => l.CreditAmount);
        if (v.TotalDebits != v.TotalCredits)
            throw new InvalidOperationException("Voucher not balanced. Debits=" + v.TotalDebits.ToString("N2") + ", Credits=" + v.TotalCredits.ToString("N2") + ".");
    }

    private async Task<int> NextSequenceAsync(int fiscalYearId, CancellationToken ct)
    {
        var max = await _financeContext.VoucherHeaders.AsNoTracking()
            .Where(v => v.FiscalYearId == fiscalYearId)
            .Select(v => (int?)v.SequenceNumber).MaxAsync(ct) ?? 0;
        return max + 1;
    }

    private async Task<FiscalYear?> ResolveFiscalYearAsync(DateTime date, int contractId, CancellationToken ct)
    {
        var dateUtc = date.Kind == DateTimeKind.Utc ? date : DateTime.SpecifyKind(date, DateTimeKind.Utc);
        var fy = await _financeContext.FiscalYears.AsNoTracking()
            .Where(x => x.StartDate <= dateUtc && x.EndDate >= dateUtc && !x.IsClosed)
            .OrderByDescending(x => x.StartDate).FirstOrDefaultAsync(ct);

        if (fy == null)
        {
            _logger.LogWarning("No open FiscalYear covers {0} for contract {1}. Raising notification alert.", dateUtc, contractId);

            // Raise an alert via NotificationService (Item 7) to the admin users
            try
            {
                var adminUserIds = await _officeContext.Users
                    .Where(u => u.IsManager)
                    .Select(u => u.Id)
                    .ToListAsync(ct);

                foreach (var uid in adminUserIds)
                {
                    await _notificationService.CreateAsync(
                        uid,
                        "سال مالی باز یافت نشد",
                        "برای قرارداد " + contractId + " در تاریخ " + dateUtc.ToString("yyyy-MM-dd") + " سال مالی باز وجود ندارد. لطفاً یک سال مالی ایجاد کنید.",
                        NotificationSeverity.Danger,
                        "/Finance/FiscalYears",
                        NotificationModule,
                        NotificationEntityType,
                        contractId,
                        null,
                        ct);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to raise notification for missing fiscal year.");
            }
        }

        return fy;
    }

    private async Task<JournalType?> ResolveJournalTypeAsync(CancellationToken ct)
    {
        var jt = await _financeContext.JournalTypes.AsNoTracking()
            .FirstOrDefaultAsync(j => j.Code == JournalTypeCodes.Treasury, ct);
        if (jt == null)
            _logger.LogWarning("JournalType '{0}' not found.", JournalTypeCodes.Treasury);
        return jt;
    }

    private async Task<SubsidiaryAccount> ResolveSubsidiaryAsync(string systemKey, string displayName, CancellationToken ct)
    {
        var account = await _financeContext.SubsidiaryAccounts
            .FirstOrDefaultAsync(a => a.SystemKey == systemKey && a.IsActive, ct);
        if (account != null) return account;

        var general = await _financeContext.GeneralAccounts.FirstOrDefaultAsync(g => g.IsActive, ct);
        if (general == null)
        {
            var group = await _financeContext.AccountGroups.FirstOrDefaultAsync(g => g.IsActive, ct);
            if (group == null)
            {
                group = new AccountGroup { Code = "1", Name = "گروه پيش‌فرض دارايی‌ها", IsActive = true, Nature = AccountNature.Debit };
                _financeContext.AccountGroups.Add(group);
                await _financeContext.SaveChangesAsync(ct);
            }
            general = new GeneralAccount { AccountGroupId = group.Id, Code = "110", Name = "حساب کل پيش‌فرض", IsActive = true, Nature = AccountNature.Debit };
            _financeContext.GeneralAccounts.Add(general);
            await _financeContext.SaveChangesAsync(ct);
        }

        var suffix = Random.Shared.Next(1000, 9999);
        account = new SubsidiaryAccount
        {
            GeneralAccountId = general.Id, Code = "110" + suffix, Name = displayName,
            SystemKey = systemKey, IsActive = true, Nature = AccountNature.NoControl, AllowsFloatingDetail = true,
        };
        _financeContext.SubsidiaryAccounts.Add(account);
        await _financeContext.SaveChangesAsync(ct);

        _logger.LogInformation("Auto-created SubsidiaryAccount '" + systemKey + "' (Id={0})", account.Id);
        return account;
    }
}
