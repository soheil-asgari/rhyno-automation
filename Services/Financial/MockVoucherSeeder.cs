using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Modules.Finance.Domain;
using OfficeAutomation.Modules.Finance.Infrastructure.Persistence;

namespace OfficeAutomation.Services.Financial;

/// <summary>
/// Seeds balanced mock vouchers linked to the ingested COA,
/// representing all operational modules.
/// Idempotent — skips if voucher data already exists.
/// </summary>
public sealed class MockVoucherSeeder
{
    private readonly FinanceDbContext _db;

    // Journal type codes used in the system
    private static class Jt
    {
        public const string Payroll = "PR";
        public const string Treasury = "TR";
        public const string Sales = "SL";
        public const string Purchase = "PU";
        public const string Contract = "CT";
        public const string Waybill = "WB";
        public const string General = "GN";
    }

    public MockVoucherSeeder(FinanceDbContext db) => _db = db;

    public async Task SeedAsync(CancellationToken ct = default)
    {
        // Skip if data exists
        var hasData = await _db.VoucherHeaders.AnyAsync(ct);
        if (hasData) return;

        // Ensure journal types exist
        await EnsureJournalTypesAsync(ct);
        await _db.SaveChangesAsync(ct);

        var jtMap = await _db.JournalTypes.ToDictionaryAsync(j => j.Code, j => j.Id, ct);
        var subsidiaries = await _db.SubsidiaryAccounts
            .Include(s => s.GeneralAccount)
            .ThenInclude(g => g.AccountGroup)
            .ToListAsync(ct);

        // Build title->entity lookup
        var byTitle = subsidiaries.ToDictionary(s => s.Name.Trim(), s => s);
        var byCode = subsidiaries.ToDictionary(s => s.Code, s => s);

        // ── Helper: ensure we can find accounts by title substring ──
        SubsidiaryAccount Find(string keyword)
        {
            return byTitle.Values.FirstOrDefault(s =>
                s.Name.Trim().Contains(keyword, StringComparison.OrdinalIgnoreCase)
            ) ?? byTitle.Values.FirstOrDefault()!;
        }

        var now = new DateTime(2025, 6, 21); // 1404/04/01
        var rng = new Random(42);
        var fy = await _db.FiscalYears.FirstOrDefaultAsync(ct);

        var voucherNumber = 100;
        var lines = new List<VoucherLine>();

        // ── 1. Payroll: حقوق پایه + بیمه پرداختنی + مالیات حقوق ──
        var payrollVh = CreateVh(++voucherNumber, now, "پرداخت حقوق فروردین ۱۴۰۴", jtMap[Jt.Payroll], fy?.Id);
        var salaryCost = Find("هزینه حقوق پایه");
        var insurancePayable = Find("بیمه پرداختنی");
        var taxPayable = Find("مالیات حقوق");
        var bank = Find("موجودی بانکهای ریالی");

        if (salaryCost != null && insurancePayable != null && taxPayable != null && bank != null)
        {
            lines.Add(CreateVl(payrollVh, salaryCost, 850_000_000m, 0));
            lines.Add(CreateVl(payrollVh, Find("حق بن و خواربار") ?? salaryCost, 150_000_000m, 0));
            lines.Add(CreateVl(payrollVh, Find("اضافه کاری") ?? salaryCost, 120_000_000m, 0));
            lines.Add(CreateVl(payrollVh, insurancePayable, 0, 180_000_000m));
            lines.Add(CreateVl(payrollVh, taxPayable, 0, 45_000_000m));
            lines.Add(CreateVl(payrollVh, Find("حقوق پرداختنی") ?? bank, 0, 895_000_000m));
        }

        // ── 2. Treasury: دریافت از مشتری ──
        var treasuryVh = CreateVh(++voucherNumber, now.AddDays(5), "وصول چک از مشتری الف", jtMap[Jt.Treasury], fy?.Id);
        var bankAcct = Find("موجودی بانکهای ریالی");
        var recvTrade = Find("حسابهای دریافتنی");
        if (bankAcct != null && recvTrade != null)
        {
            lines.Add(CreateVl(treasuryVh, bankAcct, 320_000_000m, 0));
            lines.Add(CreateVl(treasuryVh, recvTrade, 0, 320_000_000m));
        }

        // ── 3. Sales: فروش داخلی ──
        var salesVh = CreateVh(++voucherNumber, now.AddDays(10), "فاکتور فروش ۹۸-۰۰۱", jtMap[Jt.Sales], fy?.Id);
        var salesRev = Find("فروش داخلی محصولات");
        if (salesRev != null && recvTrade != null)
        {
            lines.Add(CreateVl(salesVh, recvTrade, 560_000_000m, 0));
            lines.Add(CreateVl(salesVh, salesRev, 0, 500_000_000m));
            var vatRecv = Find("مالیات بر ارزش افزوده");
            if (vatRecv != null)
                lines.Add(CreateVl(salesVh, vatRecv, 0, 60_000_000m));
        }

        // ── 4. Purchase: خرید مواد ──
        var purchaseVh = CreateVh(++voucherNumber, now.AddDays(12), "فاکتور خرید ۹۸-۰۰۲", jtMap[Jt.Purchase], fy?.Id);
        var inventory = Find("موجودی مواد وکالا") ?? Find("موجودی مصالح ساختمانی");
        var payable = Find("بستانکاران");
        if (inventory != null && payable != null)
        {
            lines.Add(CreateVl(purchaseVh, inventory, 210_000_000m, 0));
            var vatPayable = Find("حسابهای پرداختنی-مالیات ارزش افزوده");
            if (vatPayable != null)
                lines.Add(CreateVl(purchaseVh, vatPayable, 0, 25_200_000m));
            lines.Add(CreateVl(purchaseVh, payable, 0, 235_200_000m));
        }

        // ── 5. Contract: صورت‌وضعیت پیمانکاری ──
        var contractVh = CreateVh(++voucherNumber, now.AddDays(15), "صورت وضعیت پیمان ۱۴۰۴-۰۱", jtMap[Jt.Contract], fy?.Id);
        var wipCost = Find("بهای تمام شده کار انجام شده پیمانکاری");
        var receivables = Find("صورت وضعیتها");
        if (wipCost != null && receivables != null)
        {
            lines.Add(CreateVl(contractVh, wipCost, 980_000_000m, 0));
            lines.Add(CreateVl(contractVh, receivables, 980_000_000m, 0));
        }

        // ── 6. Waybill: بارنامه ──
        var waybillVh = CreateVh(++voucherNumber, now.AddDays(18), "بارنامه ۹۸-۰۰۳", jtMap[Jt.Waybill], fy?.Id);
        var freightCost = Find("هزینه حمل و نقل و عوارض");
        var carrierPay = Find("بستانکاران") ?? Find("حسابهای پرداختنی");
        if (freightCost != null && carrierPay != null)
        {
            lines.Add(CreateVl(waybillVh, freightCost, 45_000_000m, 0));
            lines.Add(CreateVl(waybillVh, carrierPay, 0, 45_000_000m));
        }

        // ── 7. General: سند افتتاحیه ──
        var openingVh = CreateVh(++voucherNumber, now.AddDays(-1), "سند افتتاحیه ابتدای دوره", jtMap[Jt.General], fy?.Id);
        var capital = Find("سرمایه");
        var cash = Find("صندوق");
        if (capital != null && cash != null)
        {
            lines.Add(CreateVl(openingVh, capital, 0, 5_000_000_000m));
            lines.Add(CreateVl(openingVh, cash, 2_000_000_000m, 0));
            lines.Add(CreateVl(openingVh, recvTrade ?? cash, 1_800_000_000m, 0));
            lines.Add(CreateVl(openingVh, inventory ?? cash, 1_200_000_000m, 0));
        }

        // ── Persist ──
        foreach (var vh in lines.Select(l => l.VoucherHeader).Distinct())
            _db.VoucherHeaders.Add(vh);

        _db.VoucherLines.AddRange(lines);
        await _db.SaveChangesAsync(ct);
    }

    private VoucherHeader CreateVh(int number, DateTime date, string desc, int journalTypeId, int? fiscalYearId)
    {
        return new VoucherHeader
        {
            VoucherNumber = number,
            VoucherDate = date,
            Description = desc,
            JournalTypeId = journalTypeId,
            Status = VoucherStatus.Permanent,
            FiscalYearId = fiscalYearId ?? 1,
            IsMemo = false,
        };
    }

    private static VoucherLine CreateVl(VoucherHeader vh, SubsidiaryAccount subsidiary, decimal debit, decimal credit)
    {
        return new VoucherLine
        {
            VoucherHeader = vh,
            SubsidiaryAccountId = subsidiary.Id,
            DebitAmount = debit,
            CreditAmount = credit,
            Narration = vh.Description,
        };
    }

    private async Task EnsureJournalTypesAsync(CancellationToken ct)
    {
        var types = new (string Code, string Name)[]
        {
            (Jt.Payroll, "حقوق و دستمزد"),
            (Jt.Treasury, "صندوق و بانک"),
            (Jt.Sales, "فروش"),
            (Jt.Purchase, "خرید"),
            (Jt.Contract, "قرارداد"),
            (Jt.Waybill, "بارنامه"),
            (Jt.General, "عمومی"),
        };

        foreach (var (code, name) in types)
        {
            var exists = await _db.JournalTypes.AnyAsync(j => j.Code == code, ct);
            if (!exists)
            {
                _db.JournalTypes.Add(new JournalType
                {
                    Code = code,
                    Name = name,
                    IsActive = true,
                });
            }
        }

        await _db.SaveChangesAsync(ct);
    }
}