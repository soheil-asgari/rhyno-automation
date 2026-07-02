using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using OfficeAutomation.Data;
using OfficeAutomation.Models;
using OfficeAutomation.Modules.Finance.Domain;
using OfficeAutomation.Services.Tenancy;

namespace OfficeAutomation.Modules.Finance.Infrastructure.Persistence;

public sealed class FinanceDbContext : ModularDbContext
{
    public FinanceDbContext(
        DbContextOptions<FinanceDbContext> options,
        ITenantIsolationService? tenantIsolationService = null)
        : base(options, tenantIsolationService)
    {
    }

    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceItem> InvoiceItems => Set<InvoiceItem>();
    public DbSet<Waybill> Waybills => Set<Waybill>();
    public DbSet<Vendor> Vendors => Set<Vendor>();
    public DbSet<Employer> Employers => Set<Employer>();
    public DbSet<InsuranceList> InsuranceLists => Set<InsuranceList>();
    public DbSet<InsuranceEmployee> InsuranceEmployees => Set<InsuranceEmployee>();
    public DbSet<PayrollList> PayrollLists => Set<PayrollList>();
    public DbSet<PayrollItem> PayrollItems => Set<PayrollItem>();
    public DbSet<FiscalYear> FiscalYears => Set<FiscalYear>();
    public DbSet<FiscalPeriod> FiscalPeriods => Set<FiscalPeriod>();
    public DbSet<AccountGroup> AccountGroups => Set<AccountGroup>();
    public DbSet<GeneralAccount> GeneralAccounts => Set<GeneralAccount>();
    public DbSet<SubsidiaryAccount> SubsidiaryAccounts => Set<SubsidiaryAccount>();
    public DbSet<DetailedAccount> DetailedAccounts => Set<DetailedAccount>();
    public DbSet<FloatingDetailAccount> FloatingDetailAccounts => Set<FloatingDetailAccount>();
    public DbSet<SubsidiaryAccountFloatingDetail> SubsidiaryAccountFloatingDetails => Set<SubsidiaryAccountFloatingDetail>();
    public DbSet<JournalType> JournalTypes => Set<JournalType>();
    public DbSet<Currency> Currencies => Set<Currency>();
    public DbSet<CurrencyExchangeRate> CurrencyExchangeRates => Set<CurrencyExchangeRate>();
    public DbSet<VoucherHeader> VoucherHeaders => Set<VoucherHeader>();
    public DbSet<VoucherLine> VoucherLines => Set<VoucherLine>();
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        AssignSequenceNumbers();
        NormalizeForeignCurrencyLines();
        EnsureFiscalPeriodsAreOpen();
        EnsureOpenFiscalYears();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return SaveChangesAsync(true, cancellationToken);
    }

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        AssignSequenceNumbers();
        NormalizeForeignCurrencyLines();
        await EnsureFiscalPeriodsAreOpenAsync(cancellationToken);
        await EnsureOpenFiscalYearsAsync(cancellationToken);
        return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ApplyTenantSchema(modelBuilder);

        modelBuilder.Ignore<IdentityUserRole<string>>();
        modelBuilder.Ignore<IdentityUserClaim<string>>();
        modelBuilder.Ignore<IdentityUserLogin<string>>();
        modelBuilder.Ignore<IdentityUserToken<string>>();
        modelBuilder.Ignore<IdentityRoleClaim<string>>();

        modelBuilder.Ignore<Department>();
        modelBuilder.Ignore<User>();
        modelBuilder.Ignore<HumanCapitalEmployee>();
        modelBuilder.Ignore<Product>();
        modelBuilder.Ignore<WarehouseReceipt>();
        modelBuilder.Ignore<WarehouseReceiptItem>();
        modelBuilder.Ignore<InventoryStock>();

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(FinanceDbContext).Assembly,
            type => type.Namespace?.StartsWith("OfficeAutomation.Modules.Finance.", StringComparison.Ordinal) == true);
    }

    private async Task EnsureOpenFiscalYearsAsync(CancellationToken cancellationToken)
    {
        EnsureVoucherLifecycleGuards();

        var fiscalYearIds = ChangeTracker.Entries()
            .Where(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Select(entry => entry.Entity switch
            {
                VoucherHeader header => header.FiscalYearId,
                VoucherLine line => line.VoucherHeader?.FiscalYearId ?? 0,
                _ => 0
            })
            .Where(id => id > 0)
            .Distinct()
            .ToList();

        if (fiscalYearIds.Count == 0)
        {
            return;
        }

        var closedYearNames = await FiscalYears
            .AsNoTracking()
            .Where(item => fiscalYearIds.Contains(item.Id) && item.IsClosed)
            .Select(item => item.YearName)
            .ToListAsync(cancellationToken);

        if (closedYearNames.Count > 0)
        {
            throw new InvalidOperationException(
                $"Cannot write accounting vouchers in closed fiscal year(s): {string.Join(", ", closedYearNames)}.");
        }
    }

    private void NormalizeForeignCurrencyLines()
    {
        var changedLines = ChangeTracker.Entries<VoucherLine>()
            .Where(entry => entry.State is EntityState.Added or EntityState.Modified)
            .Select(entry => entry.Entity);

        foreach (var line in changedLines)
        {
            if (!line.CurrencyId.HasValue)
            {
                line.ExchangeRate = line.ExchangeRate <= 0 ? 1m : line.ExchangeRate;
                continue;
            }

            if (line.ExchangeRate <= 0)
            {
                throw new InvalidOperationException("Foreign currency voucher lines must include a positive ExchangeRate.");
            }

            if (!line.ForeignAmount.HasValue)
            {
                continue;
            }

            var baseAmount = Math.Round(Math.Abs(line.ForeignAmount.Value) * line.ExchangeRate, 2, MidpointRounding.AwayFromZero);
            if (line.DebitAmount > 0 && line.CreditAmount == 0)
            {
                line.DebitAmount = baseAmount;
            }
            else if (line.CreditAmount > 0 && line.DebitAmount == 0)
            {
                line.CreditAmount = baseAmount;
            }
            else if (line.ForeignAmount.Value >= 0)
            {
                line.DebitAmount = baseAmount;
                line.CreditAmount = 0;
            }
            else
            {
                line.DebitAmount = 0;
                line.CreditAmount = baseAmount;
            }
        }
    }

    private async Task EnsureFiscalPeriodsAreOpenAsync(CancellationToken cancellationToken)
    {
        var voucherDates = GetChangedVoucherDates();
        if (voucherDates.Count == 0)
        {
            return;
        }

        var minDate = voucherDates.Min();
        var maxDate = voucherDates.Max();
        var lockedPeriods = await FiscalPeriods
            .AsNoTracking()
            .Where(item =>
                item.StartDate <= maxDate &&
                item.EndDate >= minDate &&
                (item.Status == FiscalPeriodStatus.SoftLocked || item.Status == FiscalPeriodStatus.HardLocked))
            .Select(item => new { item.Name, item.Status, item.StartDate, item.EndDate })
            .ToListAsync(cancellationToken);

        var lockedPeriod = lockedPeriods
            .FirstOrDefault(period => voucherDates.Any(date => period.StartDate <= date && period.EndDate >= date));
        if (lockedPeriod != null)
        {
            throw new InvalidOperationException(
                $"Fiscal period '{lockedPeriod.Name}' is {lockedPeriod.Status}; voucher changes are not allowed in this date range.");
        }
    }

    private void EnsureFiscalPeriodsAreOpen()
    {
        var voucherDates = GetChangedVoucherDates();
        if (voucherDates.Count == 0)
        {
            return;
        }

        var minDate = voucherDates.Min();
        var maxDate = voucherDates.Max();
        var lockedPeriods = FiscalPeriods
            .AsNoTracking()
            .Where(item =>
                item.StartDate <= maxDate &&
                item.EndDate >= minDate &&
                (item.Status == FiscalPeriodStatus.SoftLocked || item.Status == FiscalPeriodStatus.HardLocked))
            .Select(item => new { item.Name, item.Status, item.StartDate, item.EndDate })
            .ToList();

        var lockedPeriod = lockedPeriods
            .FirstOrDefault(period => voucherDates.Any(date => period.StartDate <= date && period.EndDate >= date));
        if (lockedPeriod != null)
        {
            throw new InvalidOperationException(
                $"Fiscal period '{lockedPeriod.Name}' is {lockedPeriod.Status}; voucher changes are not allowed in this date range.");
        }
    }

    private List<DateTime> GetChangedVoucherDates()
    {
        var dates = ChangeTracker.Entries<VoucherHeader>()
            .Where(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Select(entry => entry.State == EntityState.Deleted
                ? entry.OriginalValues.GetValue<DateTime>(nameof(VoucherHeader.VoucherDate)).Date
                : entry.Entity.VoucherDate.Date)
            .ToList();

        var lineHeaderIds = ChangeTracker.Entries<VoucherLine>()
            .Where(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Select(entry => entry.Entity.VoucherHeaderId)
            .Where(id => id > 0)
            .Distinct()
            .ToList();

        foreach (var headerId in lineHeaderIds)
        {
            var trackedHeader = ChangeTracker.Entries<VoucherHeader>()
                .FirstOrDefault(entry => entry.Entity.Id == headerId);
            if (trackedHeader != null)
            {
                dates.Add((trackedHeader.State == EntityState.Deleted
                    ? trackedHeader.OriginalValues.GetValue<DateTime>(nameof(VoucherHeader.VoucherDate))
                    : trackedHeader.Entity.VoucherDate).Date);
                continue;
            }

            var voucherDate = VoucherHeaders
                .AsNoTracking()
                .Where(item => item.Id == headerId)
                .Select(item => item.VoucherDate)
                .FirstOrDefault();
            if (voucherDate != default)
            {
                dates.Add(voucherDate.Date);
            }
        }

        return dates.Distinct().ToList();
    }

    private void EnsureOpenFiscalYears()
    {
        EnsureVoucherLifecycleGuards();

        var fiscalYearIds = ChangeTracker.Entries()
            .Where(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Select(entry => entry.Entity switch
            {
                VoucherHeader header => header.FiscalYearId,
                VoucherLine line => line.VoucherHeader?.FiscalYearId ?? 0,
                _ => 0
            })
            .Where(id => id > 0)
            .Distinct()
            .ToList();

        if (fiscalYearIds.Count == 0)
        {
            return;
        }

        var closedYearNames = FiscalYears
            .AsNoTracking()
            .Where(item => fiscalYearIds.Contains(item.Id) && item.IsClosed)
            .Select(item => item.YearName)
            .ToList();

        if (closedYearNames.Count > 0)
        {
            throw new InvalidOperationException(
                $"Cannot write accounting vouchers in closed fiscal year(s): {string.Join(", ", closedYearNames)}.");
        }
    }

    private void AssignSequenceNumbers()
    {
        var addedHeaders = ChangeTracker.Entries<VoucherHeader>()
            .Where(entry => entry.State == EntityState.Added)
            .Select(entry => entry.Entity)
            .Where(item => item.SequenceNumber <= 0 && item.FiscalYearId > 0)
            .GroupBy(item => item.FiscalYearId)
            .ToList();

        foreach (var fiscalYearGroup in addedHeaders)
        {
            var nextSequence = VoucherHeaders
                .AsNoTracking()
                .Where(item => item.FiscalYearId == fiscalYearGroup.Key)
                .Select(item => (int?)item.SequenceNumber)
                .Max() ?? 0;

            foreach (var voucher in fiscalYearGroup.OrderBy(item => item.VoucherDate).ThenBy(item => item.Id))
            {
                nextSequence++;
                voucher.SequenceNumber = nextSequence;
                voucher.VoucherNumber ??= nextSequence;
            }
        }
    }

    private void EnsureVoucherLifecycleGuards()
    {
        var modifiedPostedHeaders = ChangeTracker.Entries<VoucherHeader>()
            .Where(entry =>
                entry.State is EntityState.Modified or EntityState.Deleted &&
                string.Equals(entry.OriginalValues.GetValue<string>(nameof(VoucherHeader.PostingStatus)), PostingStatus.Posted, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (modifiedPostedHeaders.Count > 0)
        {
            throw new InvalidOperationException("Posted accounting vouchers are immutable. Create a reversal voucher instead.");
        }

        var modifiedPermanentHeaders = ChangeTracker.Entries<VoucherHeader>()
            .Where(entry =>
                entry.State is EntityState.Modified or EntityState.Deleted &&
                entry.OriginalValues.GetValue<VoucherStatus>(nameof(VoucherHeader.Status)) == VoucherStatus.Permanent)
            .ToList();

        if (modifiedPermanentHeaders.Count > 0)
        {
            throw new InvalidOperationException("Permanent accounting vouchers are immutable.");
        }

        var sequenceChangedHeader = ChangeTracker.Entries<VoucherHeader>()
            .FirstOrDefault(entry =>
                entry.State == EntityState.Modified &&
                entry.OriginalValues.GetValue<int>(nameof(VoucherHeader.SequenceNumber)) != entry.CurrentValues.GetValue<int>(nameof(VoucherHeader.SequenceNumber)));

        if (sequenceChangedHeader != null)
        {
            throw new InvalidOperationException("SequenceNumber is immutable after voucher creation.");
        }

        var modifiedLines = ChangeTracker.Entries<VoucherLine>()
            .Where(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();

        foreach (var lineEntry in modifiedLines)
        {
            var voucherId = lineEntry.Entity.VoucherHeaderId;
            EntityEntry<VoucherHeader>? headerEntry = null;
            if (lineEntry.Entity.VoucherHeader != null)
            {
                headerEntry = Entry(lineEntry.Entity.VoucherHeader);
            }

            headerEntry ??= ChangeTracker.Entries<VoucherHeader>()
                .FirstOrDefault(entry => entry.Entity.Id == voucherId);

            if (lineEntry.State == EntityState.Added &&
                ((headerEntry != null && headerEntry.State == EntityState.Added) || voucherId <= 0))
            {
                continue;
            }

            if (headerEntry?.State == EntityState.Added)
            {
                continue;
            }

            var isPosted = headerEntry != null
                ? string.Equals(headerEntry.OriginalValues.GetValue<string>(nameof(VoucherHeader.PostingStatus)), PostingStatus.Posted, StringComparison.OrdinalIgnoreCase)
                : VoucherHeaders.AsNoTracking().Any(item => item.Id == voucherId && item.PostingStatus == PostingStatus.Posted);

            if (isPosted)
            {
                throw new InvalidOperationException("Posted accounting voucher lines are immutable. Create a reversal voucher instead.");
            }

            var isPermanent = headerEntry != null
                ? headerEntry.OriginalValues.GetValue<VoucherStatus>(nameof(VoucherHeader.Status)) == VoucherStatus.Permanent
                : VoucherHeaders.AsNoTracking().Any(item => item.Id == voucherId && item.Status == VoucherStatus.Permanent);

            if (isPermanent)
            {
                throw new InvalidOperationException("Permanent accounting voucher lines are immutable.");
            }
        }
    }
}
