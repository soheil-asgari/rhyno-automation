using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Modules.Finance.Domain;
using OfficeAutomation.Modules.Finance.Infrastructure.Persistence;

namespace OfficeAutomation.Modules.Finance.Application;

public sealed class VoucherRenumberingService
{
    private readonly FinanceDbContext _context;

    public VoucherRenumberingService(FinanceDbContext context)
    {
        _context = context;
    }

    public async Task<int> RenumberAsync(Guid fiscalPeriodId, CancellationToken cancellationToken = default)
    {
        var period = await _context.FiscalPeriods
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == fiscalPeriodId, cancellationToken)
            ?? throw new InvalidOperationException($"Fiscal period '{fiscalPeriodId}' was not found.");

        var vouchers = await _context.VoucherHeaders
            .Where(item =>
                item.VoucherDate >= period.StartDate.Date &&
                item.VoucherDate <= period.EndDate.Date &&
                item.Status != VoucherStatus.Permanent)
            .OrderBy(item => item.VoucherDate)
            .ThenBy(item => item.SequenceNumber)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);

        for (var index = 0; index < vouchers.Count; index++)
        {
            vouchers[index].VoucherNumber = index + 1;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return vouchers.Count;
    }
}
