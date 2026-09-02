using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Modules.Finance.Domain;
using OfficeAutomation.Modules.Finance.Infrastructure.Persistence;

namespace OfficeAutomation.Services.Financial;

public sealed class AccountReviewRowDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public decimal DebitBalance { get; set; }
    public decimal CreditBalance { get; set; }
    public decimal Balance { get; set; }
    public int Level { get; set; }
    public string Source { get; set; } = string.Empty;
    public string SourceCode { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public bool HasChildren { get; set; }
}

public sealed class AccountReviewRequest
{
    public int Level { get; set; } = 1;
    public int FromFiscalYear { get; set; }
    public int ToFiscalYear { get; set; }
    public int? AccountType { get; set; }
    public int? DetailType { get; set; }
    public List<int> VoucherTypes { get; set; } = [];
    public List<int> VoucherStates { get; set; } = [];
    public string Filter { get; set; } = "month";
    public int? FromMonth { get; set; }
    public int? ToMonth { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int? FromNumber { get; set; }
    public int? ToNumber { get; set; }
    public bool WithBalance { get; set; }
    public string? ParentCode { get; set; }
}

public sealed class AccountReviewResult
{
    public IReadOnlyList<AccountReviewRowDto> Rows { get; set; } = [];
    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
    public decimal TotalDebitBalance { get; set; }
    public decimal TotalCreditBalance { get; set; }
    public string AccountPath { get; set; } = string.Empty;
}

public sealed class TransactionRowDto
{
    public int VoucherHeaderId { get; set; }
    public int VoucherNumber { get; set; }
    public string VoucherDate { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string JournalType { get; set; } = string.Empty;
    public string AccountCode { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public decimal Balance { get; set; }
    public string SourceModule { get; set; } = string.Empty;
    public string SourceEntityType { get; set; } = string.Empty;
    public int? SourceEntityId { get; set; }
}

public sealed class SourceDocumentDto
{
    public string Module { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string DocumentNumber { get; set; } = string.Empty;
    public DateTime DocumentDate { get; set; }
    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}

public sealed class FiscalYearOptionDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public sealed class JournalTypeOptionDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public sealed class DetailedTypeOptionDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public sealed class VoucherStateOptionDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public interface IAccountReviewService
{
    Task<AccountReviewResult> GetAccountReviewAsync(AccountReviewRequest request, CancellationToken ct = default);
    Task<AccountReviewResult> GetChildAccountsAsync(AccountReviewRequest request, string parentCode, CancellationToken ct = default);
    Task<List<TransactionRowDto>> GetTransactionsAsync(int level, string code, AccountReviewRequest filters, CancellationToken ct = default);
    Task<SourceDocumentDto?> GetSourceDocumentAsync(int voucherLineId, CancellationToken ct = default);
    Task<List<FiscalYearOptionDto>> GetFiscalYearsAsync(CancellationToken ct = default);
    Task<List<JournalTypeOptionDto>> GetJournalTypesAsync(CancellationToken ct = default);
    Task<List<DetailedTypeOptionDto>> GetDetailedTypesAsync(CancellationToken ct = default);
    Task<List<VoucherStateOptionDto>> GetVoucherStatesAsync(CancellationToken ct = default);
}

/// <summary>
/// Account Review (مرور حساب‌ها) matching Rahkaran ERP layout.
/// Aggregates GL journal entries from all operational modules:
/// Sales, Purchasing, Treasury, Payroll, Logistics, Contracts.
/// </summary>
public sealed class AccountReviewService : IAccountReviewService
{
    private readonly FinanceDbContext _context;
    private static readonly int[] PermanentStates = [(int)VoucherStatus.Permanent, (int)VoucherStatus.Posted];

    public AccountReviewService(FinanceDbContext context)
    {
        _context = context;
    }

    public async Task<List<FiscalYearOptionDto>> GetFiscalYearsAsync(CancellationToken ct = default)
    {
        return await _context.FiscalYears
            .AsNoTracking()
            .OrderByDescending(item => item.YearName)
            .Select(item => new FiscalYearOptionDto { Id = item.Id, Name = item.YearName })
            .ToListAsync(ct);
    }

    public async Task<List<JournalTypeOptionDto>> GetJournalTypesAsync(CancellationToken ct = default)
    {
        return await _context.JournalTypes
            .AsNoTracking()
            .Where(item => item.IsActive)
            .OrderBy(item => item.Name)
            .Select(item => new JournalTypeOptionDto { Id = item.Id, Code = item.Code, Name = item.Name })
            .ToListAsync(ct);
    }

    public async Task<List<DetailedTypeOptionDto>> GetDetailedTypesAsync(CancellationToken ct = default)
    {
        var types = new List<DetailedTypeOptionDto>
        {
            new() { Id = 1, Name = "شخص" },
            new() { Id = 2, Name = "پروژه" },
            new() { Id = 3, Name = "مرکز هزینه" },
            new() { Id = 4, Name = "حساب بانکی" },
            new() { Id = 5, Name = "دارایی" },
            new() { Id = 6, Name = "بیمه" },
        };
        return await Task.FromResult(types);
    }

    public async Task<List<VoucherStateOptionDto>> GetVoucherStatesAsync(CancellationToken ct = default)
    {
        var states = new List<VoucherStateOptionDto>
        {
            new() { Id = (int)VoucherStatus.Draft, Name = "یادداشت" },
            new() { Id = (int)VoucherStatus.Reviewed, Name = "بررسی‌شده" },
            new() { Id = (int)VoucherStatus.Approved, Name = "موقت" },
            new() { Id = (int)VoucherStatus.Permanent, Name = "قطعی" },
            new() { Id = (int)VoucherStatus.Posted, Name = "ثبت‌شده" },
        };
        return await Task.FromResult(states);
    }

    private async Task<(DateTime from, DateTime to)> ResolveDateRangeAsync(AccountReviewRequest request, CancellationToken ct)
    {
        var fromYear = await _context.FiscalYears
            .AsNoTracking()
            .Where(item => item.YearName == request.FromFiscalYear.ToString())
            .FirstOrDefaultAsync(ct);
        var toYear = await _context.FiscalYears
            .AsNoTracking()
            .Where(item => item.YearName == request.ToFiscalYear.ToString())
            .FirstOrDefaultAsync(ct);

        var from = fromYear?.StartDate ?? DateTime.MinValue;
        var to = toYear?.EndDate ?? DateTime.MaxValue;

        if (!string.IsNullOrEmpty(request.Filter))
        {
            if (request.Filter == "month" && request.FromMonth.HasValue && request.ToMonth.HasValue)
            {
                var baseFrom = from;
                var baseTo = to;
                from = new DateTime(baseFrom.Year, Math.Clamp(request.FromMonth.Value, 1, 12), 1);
                to = new DateTime(baseTo.Year, Math.Clamp(request.ToMonth.Value, 1, 12), 1);
                to = to.AddMonths(1).AddDays(-1);
            }
            else if (request.Filter == "date" && request.FromDate.HasValue && request.ToDate.HasValue)
            {
                from = request.FromDate.Value.Date;
                to = request.ToDate.Value.Date;
            }
            else if (request.Filter == "number" && request.FromNumber.HasValue && request.ToNumber.HasValue)
            {
                // date range not restricted; number filtering applied separately
            }
        }
        return (from, to);
    }

    private IQueryable<VoucherLine> BuildBaseQuery(AccountReviewRequest request, DateTime from, DateTime to)
    {
        var query = _context.VoucherLines
            .AsNoTracking()
            .Include(l => l.VoucherHeader).ThenInclude(h => h.JournalType)
            .Include(l => l.SubsidiaryAccount).ThenInclude(s => s.GeneralAccount).ThenInclude(g => g.AccountGroup)
            .Include(l => l.DetailedAccount)
            .Include(l => l.FloatingDetailAccount)
            .Include(l => l.Currency)
            .Where(line =>
                !line.VoucherHeader.IsMemo &&
                line.VoucherHeader.VoucherDate >= from &&
                line.VoucherHeader.VoucherDate <= to);

        // Voucher state filter: default to permanent+posted
        if (request.VoucherStates is { Count: > 0 })
        {
            query = query.Where(line => request.VoucherStates.Contains((int)line.VoucherHeader.Status));
        }
        else
        {
            query = query.Where(line => line.VoucherHeader.Status == VoucherStatus.Permanent ||
                                        line.VoucherHeader.Status == VoucherStatus.Posted);
        }

        if (request.VoucherTypes is { Count: > 0 })
        {
            query = query.Where(line => request.VoucherTypes.Contains(line.VoucherHeader.JournalTypeId));
        }

        if (request.FromNumber.HasValue)
        {
            query = query.Where(line => line.VoucherHeader.VoucherNumber >= request.FromNumber.Value);
        }
        if (request.ToNumber.HasValue)
        {
            query = query.Where(line => line.VoucherHeader.VoucherNumber <= request.ToNumber.Value);
        }

        if (request.AccountType.HasValue)
        {
            var acctType = request.AccountType.Value;
            query = acctType switch
            {
                1 => query.Where(l => !l.SubsidiaryAccount.IsTemporary),
                2 => query.Where(l => l.SubsidiaryAccount.IsTemporary),
                _ => query
            };
        }

        return query;
    }

    public async Task<AccountReviewResult> GetAccountReviewAsync(AccountReviewRequest request, CancellationToken ct = default)
    {
        var (from, to) = await ResolveDateRangeAsync(request, ct);
        var query = BuildBaseQuery(request, from, to);

        var lines = await query
            .Select(line => new
            {
                line.DebitAmount,
                line.CreditAmount,
                JournalCode = line.VoucherHeader.JournalType.Code,
                JournalName = line.VoucherHeader.JournalType.Name,
                GroupCode = line.SubsidiaryAccount.GeneralAccount.AccountGroup.Code,
                GroupName = line.SubsidiaryAccount.GeneralAccount.AccountGroup.Name,
                GeneralCode = line.SubsidiaryAccount.GeneralAccount.Code,
                GeneralName = line.SubsidiaryAccount.GeneralAccount.Name,
                SubsidiaryCode = line.SubsidiaryAccount.Code,
                SubsidiaryName = line.SubsidiaryAccount.Name,
                DetailedCode = line.DetailedAccount != null ? line.DetailedAccount.Code : null,
                DetailedName = line.DetailedAccount != null ? line.DetailedAccount.Name : null,
                FloatingCode = line.FloatingDetailAccount != null ? line.FloatingDetailAccount.Code : null,
                FloatingName = line.FloatingDetailAccount != null ? line.FloatingDetailAccount.Name : null,
                CurrencyCode = line.Currency != null ? line.Currency.Code : null,
                CurrencyName = line.Currency != null ? line.Currency.Name : null,
            })
            .ToListAsync(ct);

        var grouped = lines.GroupBy(line => request.Level switch
        {
            1 => (Code: line.GroupCode, Name: line.GroupName),
            2 => (Code: line.GeneralCode, Name: line.GeneralName),
            3 => (Code: line.SubsidiaryCode, Name: line.SubsidiaryName),
            4 => (Code: line.DetailedCode ?? line.SubsidiaryCode, Name: line.DetailedName ?? line.SubsidiaryName),
            5 => (Code: line.FloatingCode ?? line.SubsidiaryCode, Name: line.FloatingName ?? line.SubsidiaryName),
            6 => (Code: line.FloatingCode ?? line.SubsidiaryCode, Name: line.FloatingName ?? line.SubsidiaryName),
            7 => (Code: line.CurrencyCode ?? line.SubsidiaryCode, Name: line.CurrencyName ?? line.SubsidiaryName),
            _ => (Code: line.SubsidiaryCode, Name: line.SubsidiaryName),
        });

        var rows = grouped
            .Select(g =>
            {
                var totalDebit = g.Sum(x => x.DebitAmount);
                var totalCredit = g.Sum(x => x.CreditAmount);
                var balance = totalDebit - totalCredit;
                return new AccountReviewRowDto
                {
                    Code = g.Key.Code ?? string.Empty,
                    Name = g.Key.Name ?? string.Empty,
                    Debit = totalDebit,
                    Credit = totalCredit,
                    DebitBalance = balance > 0 ? balance : 0,
                    CreditBalance = balance < 0 ? Math.Abs(balance) : 0,
                    Balance = balance,
                    Level = request.Level,
                    Source = string.Join("، ", g.Select(x => GetSourceName(x.JournalCode, x.JournalName)).Distinct()),
                    SourceCode = string.Join(",", g.Select(x => x.JournalCode).Distinct()),
                    Path = BuildAccountPath(g.Key.Code ?? "", request.Level),
                    HasChildren = request.Level < 6,
                };
            })
            .OrderBy(r => r.Code)
            .ToList();

        if (request.WithBalance)
        {
            rows = rows.Where(r => r.Balance != 0).ToList();
        }

        var accountPath = BuildAccountPath(rows.FirstOrDefault()?.Code ?? "", request.Level);

        return new AccountReviewResult
        {
            Rows = rows,
            TotalDebit = rows.Sum(r => r.Debit),
            TotalCredit = rows.Sum(r => r.Credit),
            TotalDebitBalance = rows.Sum(r => r.DebitBalance),
            TotalCreditBalance = rows.Sum(r => r.CreditBalance),
            AccountPath = accountPath,
        };
    }

    public async Task<AccountReviewResult> GetChildAccountsAsync(AccountReviewRequest request, string parentCode, CancellationToken ct = default)
    {
        var (from, to) = await ResolveDateRangeAsync(request, ct);
        var query = BuildBaseQuery(request, from, to);

        // Filter by parent code at the child level
        query = request.Level switch
        {
            // Current level is Group (1) -> show General (2) children
            1 => query.Where(l => l.SubsidiaryAccount.GeneralAccount.AccountGroup.Code == parentCode),
            // Current level is General (2) -> show Subsidiary (3) children
            2 => query.Where(l => l.SubsidiaryAccount.GeneralAccount.Code == parentCode),
            // Current level is Subsidiary (3) -> show Detailed (4) children
            3 => query.Where(l => l.SubsidiaryAccount.Code == parentCode),
            // Current level is Detailed (4) -> show Floating (5) children
            4 => query.Where(l => l.DetailedAccount != null && l.DetailedAccount.Code == parentCode),
            _ => query.Where(l => l.SubsidiaryAccount.Code == parentCode),
        };

        var lines = await query
            .Select(line => new
            {
                line.DebitAmount,
                line.CreditAmount,
                JournalCode = line.VoucherHeader.JournalType.Code,
                JournalName = line.VoucherHeader.JournalType.Name,
                GroupCode = line.SubsidiaryAccount.GeneralAccount.AccountGroup.Code,
                GroupName = line.SubsidiaryAccount.GeneralAccount.AccountGroup.Name,
                GeneralCode = line.SubsidiaryAccount.GeneralAccount.Code,
                GeneralName = line.SubsidiaryAccount.GeneralAccount.Name,
                SubsidiaryCode = line.SubsidiaryAccount.Code,
                SubsidiaryName = line.SubsidiaryAccount.Name,
                DetailedCode = line.DetailedAccount != null ? line.DetailedAccount.Code : null,
                DetailedName = line.DetailedAccount != null ? line.DetailedAccount.Name : null,
                FloatingCode = line.FloatingDetailAccount != null ? line.FloatingDetailAccount.Code : null,
                FloatingName = line.FloatingDetailAccount != null ? line.FloatingDetailAccount.Name : null,
                CurrencyCode = line.Currency != null ? line.Currency.Code : null,
                CurrencyName = line.Currency != null ? line.Currency.Name : null,
            })
            .ToListAsync(ct);

        var childLevel = Math.Min(request.Level + 1, 7);
        var grouped = lines.GroupBy(line => childLevel switch
        {
            2 => (Code: line.GeneralCode, Name: line.GeneralName),
            3 => (Code: line.SubsidiaryCode, Name: line.SubsidiaryName),
            4 => (Code: line.DetailedCode ?? line.SubsidiaryCode, Name: line.DetailedName ?? line.SubsidiaryName),
            5 => (Code: line.FloatingCode ?? line.SubsidiaryCode, Name: line.FloatingName ?? line.SubsidiaryName),
            _ => (Code: line.SubsidiaryCode, Name: line.SubsidiaryName),
        });

        var rows = grouped
            .Select(g =>
            {
                var totalDebit = g.Sum(x => x.DebitAmount);
                var totalCredit = g.Sum(x => x.CreditAmount);
                var balance = totalDebit - totalCredit;
                return new AccountReviewRowDto
                {
                    Code = g.Key.Code ?? string.Empty,
                    Name = g.Key.Name ?? string.Empty,
                    Debit = totalDebit,
                    Credit = totalCredit,
                    DebitBalance = balance > 0 ? balance : 0,
                    CreditBalance = balance < 0 ? Math.Abs(balance) : 0,
                    Balance = balance,
                    Level = childLevel,
                    Source = string.Join("، ", g.Select(x => GetSourceName(x.JournalCode, x.JournalName)).Distinct()),
                    SourceCode = string.Join(",", g.Select(x => x.JournalCode).Distinct()),
                    Path = BuildAccountPath(g.Key.Code ?? "", childLevel),
                    HasChildren = childLevel < 6,
                };
            })
            .OrderBy(r => r.Code)
            .ToList();

        return new AccountReviewResult
        {
            Rows = rows,
            TotalDebit = rows.Sum(r => r.Debit),
            TotalCredit = rows.Sum(r => r.Credit),
            TotalDebitBalance = rows.Sum(r => r.DebitBalance),
            TotalCreditBalance = rows.Sum(r => r.CreditBalance),
            AccountPath = BuildAccountPath(parentCode, childLevel),
        };
    }

    public async Task<List<TransactionRowDto>> GetTransactionsAsync(int level, string code, AccountReviewRequest filters, CancellationToken ct = default)
    {
        var (from, to) = await ResolveDateRangeAsync(filters, ct);
        var query = BuildBaseQuery(filters, from, to);

        query = level switch
        {
            1 => query.Where(l => l.SubsidiaryAccount.GeneralAccount.AccountGroup.Code == code),
            2 => query.Where(l => l.SubsidiaryAccount.GeneralAccount.Code == code),
            3 => query.Where(l => l.SubsidiaryAccount.Code == code),
            4 => query.Where(l => l.DetailedAccount != null && l.DetailedAccount.Code == code),
            5 => query.Where(l => l.FloatingDetailAccount != null && l.FloatingDetailAccount.Code == code),
            6 => query.Where(l => l.FloatingDetailAccount != null && l.FloatingDetailAccount.Code == code),
            7 => query.Where(l => l.Currency != null && l.Currency.Code == code),
            _ => query.Where(l => l.SubsidiaryAccount.Code == code),
        };

        var transactions = await query
            .OrderByDescending(l => l.VoucherHeader.VoucherDate)
            .ThenBy(l => l.VoucherHeader.VoucherNumber)
            .Select(l => new TransactionRowDto
            {
                VoucherHeaderId = l.VoucherHeaderId,
                VoucherNumber = l.VoucherHeader.VoucherNumber ?? 0,
                VoucherDate = l.VoucherHeader.VoucherDate.ToString("yyyy/MM/dd"),
                Description = l.Narration ?? l.VoucherHeader.Description ?? "",
                JournalType = l.VoucherHeader.JournalType.Name,
                AccountCode = l.SubsidiaryAccount.Code,
                AccountName = l.SubsidiaryAccount.Name,
                Debit = l.DebitAmount,
                Credit = l.CreditAmount,
                Balance = l.DebitAmount - l.CreditAmount,
                SourceModule = l.VoucherHeader.JournalType.Code,
                SourceEntityType = GetSourceEntityType(l.VoucherHeader.JournalType.Code),
                SourceEntityId = null,
            })
            .ToListAsync(ct);

        return transactions;
    }

    public async Task<SourceDocumentDto?> GetSourceDocumentAsync(int voucherLineId, CancellationToken ct = default)
    {
        var line = await _context.VoucherLines
            .AsNoTracking()
            .Include(l => l.VoucherHeader).ThenInclude(h => h.JournalType)
            .FirstOrDefaultAsync(l => l.Id == voucherLineId, ct);

        if (line is null) return null;

        var jtCode = line.VoucherHeader.JournalType.Code;
        var module = GetSourceName(jtCode, "");
        var entityType = GetSourceEntityType(jtCode);

        return new SourceDocumentDto
        {
            Module = module,
            EntityType = entityType,
            DocumentNumber = line.VoucherHeader.DocumentNumber,
            DocumentDate = line.VoucherHeader.VoucherDate,
            Amount = line.DebitAmount > 0 ? line.DebitAmount : line.CreditAmount,
            Description = line.Narration ?? line.VoucherHeader.Description ?? "",
            Url = BuildSourceUrl(jtCode, line.VoucherHeader.DocumentNumber),
        };
    }

    private static string BuildSourceUrl(string journalCode, string documentNumber)
    {
        return journalCode switch
        {
            JournalTypeCodes.Sales => $"/Financial/Invoices?search={documentNumber}",
            JournalTypeCodes.Purchase => $"/Financial/Invoices?search={documentNumber}",
            JournalTypeCodes.Treasury => $"/Treasury/Index?search={documentNumber}",
            JournalTypeCodes.Payroll => $"/Payroll/Index?search={documentNumber}",
            _ => "",
        };
    }

    private static string BuildAccountPath(string code, int level)
    {
        var levelName = level switch
        {
            1 => "گروه",
            2 => "کل",
            3 => "معین",
            4 => "سطح چهار",
            5 => "سطح پنج",
            6 => "سطح شش",
            7 => "ارز",
            8 => "پیگیری",
            9 => "حساب تفصیلی",
            _ => "حساب",
        };
        return $"{levelName}: {code}";
    }

    private static string GetSourceEntityType(string journalCode)
    {
        return journalCode switch
        {
            JournalTypeCodes.Sales => "فاکتور فروش",
            JournalTypeCodes.Purchase => "فاکتور خرید",
            JournalTypeCodes.Treasury => "دریافت/پرداخت",
            JournalTypeCodes.Payroll => "حقوق و دستمزد",
            JournalTypeCodes.General => "سند عمومی",
            JournalTypeCodes.Closing => "بستن حساب",
            JournalTypeCodes.Opening => "افتتاحیه",
            JournalTypeCodes.Adjustment => "سند اصلاحی",
            JournalTypeCodes.Contract => "قرارداد",
            JournalTypeCodes.Waybill => "بارنامه",
            JournalTypeCodes.Insurance => "حقوق و دستمزد",
            _ => "سند عمومی",
        };
    }

    private static string GetSourceName(string code, string fallback)
    {
        return code switch
        {
            JournalTypeCodes.Sales => "فاکتور فروش",
            JournalTypeCodes.Purchase => "فاکتور خرید",
            JournalTypeCodes.Treasury => "صندوق و بانک",
            JournalTypeCodes.Payroll => "حقوق و دستمزد",
            JournalTypeCodes.General => "سند عمومی",
            JournalTypeCodes.Closing => "بستن حساب‌ها",
            JournalTypeCodes.Opening => "افتتاحیه",
            JournalTypeCodes.Adjustment => "سند اصلاحی",
            JournalTypeCodes.Contract => "قراردادها",
            JournalTypeCodes.Waybill => "بارنامه و حمل‌ونقل",
            JournalTypeCodes.Insurance => "بیمه",
            _ => fallback
        };
    }
}