using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Modules.Finance.Domain;
using OfficeAutomation.Modules.Finance.Infrastructure.Persistence;

namespace OfficeAutomation.Modules.Finance.Application;

public sealed class VoucherValidationGuard
{
    private readonly FinanceDbContext _context;

    public VoucherValidationGuard(FinanceDbContext context)
    {
        _context = context;
    }

    public async Task ValidateAsync(VoucherHeader voucher, CancellationToken cancellationToken = default)
    {
        if (voucher.Lines.Count == 0)
        {
            return;
        }

        var subsidiaryAccountIds = voucher.Lines
            .Select(item => item.SubsidiaryAccountId)
            .Where(id => id > 0)
            .Distinct()
            .ToList();
        var floatingDetailIds = voucher.Lines
            .Where(item => item.FloatingDetailAccountId.HasValue)
            .Select(item => item.FloatingDetailAccountId!.Value)
            .Distinct()
            .ToList();

        var subsidiaries = await _context.SubsidiaryAccounts
            .AsNoTracking()
            .Where(item => subsidiaryAccountIds.Contains(item.Id))
            .Select(item => new SubsidiaryLookup(
                item.Id,
                item.Code,
                item.Name,
                item.IsActive,
                item.Nature,
                item.GeneralAccount.Nature))
            .ToDictionaryAsync(item => item.Id, cancellationToken);

        var missingSubsidiaryId = subsidiaryAccountIds.FirstOrDefault(id => !subsidiaries.ContainsKey(id));
        if (missingSubsidiaryId > 0)
        {
            throw new InvalidOperationException($"Subsidiary account '{missingSubsidiaryId}' was not found.");
        }

        var inactiveSubsidiary = subsidiaries.Values.FirstOrDefault(item => !item.IsActive);
        if (inactiveSubsidiary != null)
        {
            throw new InvalidOperationException(
                $"حساب معین غیرفعال است و امکان استفاده در سند را ندارد: {inactiveSubsidiary.Code} - {inactiveSubsidiary.Name}.");
        }

        if (floatingDetailIds.Count > 0)
        {
            var floatingDetails = await _context.FloatingDetailAccounts
                .AsNoTracking()
                .Where(item => floatingDetailIds.Contains(item.Id))
                .Select(item => new FloatingDetailLookup(item.Id, item.Code, item.Name, item.IsActive))
                .ToDictionaryAsync(item => item.Id, cancellationToken);

            var missingFloatingId = floatingDetailIds.FirstOrDefault(id => !floatingDetails.ContainsKey(id));
            if (missingFloatingId != Guid.Empty)
            {
                throw new InvalidOperationException($"Floating detail '{missingFloatingId}' was not found.");
            }

            var inactiveFloating = floatingDetails.Values.FirstOrDefault(item => !item.IsActive);
            if (inactiveFloating != null)
            {
                throw new InvalidOperationException(
                    $"تفصیلی شناور غیرفعال است و امکان استفاده در سند را ندارد: {inactiveFloating.Code} - {inactiveFloating.Name}.");
            }
        }

        var balances = await _context.VoucherLines
            .AsNoTracking()
            .Where(line =>
                subsidiaryAccountIds.Contains(line.SubsidiaryAccountId) &&
                line.VoucherHeader.TotalDebits == line.VoucherHeader.TotalCredits &&
                (voucher.Id <= 0 || line.VoucherHeaderId != voucher.Id))
            .GroupBy(line => line.SubsidiaryAccountId)
            .Select(group => new
            {
                SubsidiaryAccountId = group.Key,
                Debit = group.Sum(line => line.DebitAmount),
                Credit = group.Sum(line => line.CreditAmount)
            })
            .ToDictionaryAsync(item => item.SubsidiaryAccountId, cancellationToken);

        var currentVoucherEffect = voucher.Lines
            .GroupBy(item => item.SubsidiaryAccountId)
            .ToDictionary(
                group => group.Key,
                group => new Balance(group.Sum(item => item.DebitAmount), group.Sum(item => item.CreditAmount)));

        foreach (var subsidiary in subsidiaries.Values)
        {
            var controlNature = subsidiary.Nature != AccountNature.NoControl
                ? subsidiary.Nature
                : subsidiary.GeneralNature;
            if (controlNature == AccountNature.NoControl)
            {
                continue;
            }

            balances.TryGetValue(subsidiary.Id, out var persistedBalance);
            currentVoucherEffect.TryGetValue(subsidiary.Id, out var currentBalance);

            var finalDebit = (persistedBalance?.Debit ?? 0m) + (currentBalance?.Debit ?? 0m);
            var finalCredit = (persistedBalance?.Credit ?? 0m) + (currentBalance?.Credit ?? 0m);
            var net = finalDebit - finalCredit;

            if (controlNature == AccountNature.Debit && net < 0m)
            {
                throw new InvalidOperationException(
                    $"کنترل ماهیت حساب اجازه نمی‌دهد حساب بدهکار اصیل بستانکار شود: {subsidiary.Code} - {subsidiary.Name}.");
            }

            if (controlNature == AccountNature.Credit && net > 0m)
            {
                throw new InvalidOperationException(
                    $"کنترل ماهیت حساب اجازه نمی‌دهد حساب بستانکار اصیل بدهکار شود: {subsidiary.Code} - {subsidiary.Name}.");
            }
        }
    }

    private sealed record SubsidiaryLookup(
        int Id,
        string Code,
        string Name,
        bool IsActive,
        AccountNature Nature,
        AccountNature GeneralNature);

    private sealed record FloatingDetailLookup(Guid Id, string Code, string Name, bool IsActive);
    private sealed record Balance(decimal Debit, decimal Credit);
}
