using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Modules.Finance.Domain;
using OfficeAutomation.Modules.Finance.Infrastructure.Persistence;

namespace OfficeAutomation.Modules.Finance.Application;

/// <summary>
/// Handles reopening locked/closed fiscal periods with full audit trail logging.
/// Requires explicit reason and authorization when changing from Closed/Locked back to Open.
/// </summary>
public sealed class FiscalPeriodReopeningService
{
    private readonly FinanceDbContext _context;

    public FiscalPeriodReopeningService(FinanceDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Changes a fiscal period status. When changing from a locked/closed status to Open,
    /// an explicit reason and userId are required. The event is logged in FiscalPeriodAuditLog.
    /// </summary>
    public async Task<FiscalPeriod> ReopenFiscalPeriodAsync(
        Guid fiscalPeriodId,
        string newStatus,
        string userId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new InvalidOperationException("User ID is required for fiscal period status changes.");
        }

        var period = await _context.FiscalPeriods
            .Include(item => item.FiscalYear)
            .FirstOrDefaultAsync(item => item.Id == fiscalPeriodId, cancellationToken)
            ?? throw new InvalidOperationException($"Fiscal period '{fiscalPeriodId}' was not found.");

        if (newStatus is not (FiscalPeriodStatus.Open or FiscalPeriodStatus.SoftLocked or FiscalPeriodStatus.HardLocked))
        {
            throw new InvalidOperationException($"Invalid target status '{newStatus}'.");
        }

        var previousStatus = period.Status;

        if (previousStatus == newStatus)
        {
            return period;
        }

        // Reopening from Closed/Locked to Open requires explicit reason
        bool isReopening = previousStatus is FiscalPeriodStatus.SoftLocked or FiscalPeriodStatus.HardLocked
                           && newStatus == FiscalPeriodStatus.Open;

        if (isReopening)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new InvalidOperationException("An explicit audit reason is required when reopening a locked or closed fiscal period.");
            }
        }

        // For fiscal year: if the fiscal year itself is closed, we also need to check
        if (period.FiscalYear.IsClosed && newStatus == FiscalPeriodStatus.Open)
        {
            throw new InvalidOperationException(
                $"Fiscal year '{period.FiscalYear.YearName}' is closed. Reopen the fiscal year before reopening individual periods.");
        }

        period.Status = newStatus;

        // Log the audit trail
        var auditLog = new FiscalPeriodAuditLog
        {
            FiscalPeriodId = fiscalPeriodId,
            FiscalYearId = period.FiscalYearId,
            UserId = userId,
            Timestamp = DateTimeOffset.UtcNow,
            PreviousStatus = previousStatus,
            NewStatus = newStatus,
            Reason = isReopening ? reason.Trim() : $"Status changed from {previousStatus} to {newStatus}"
        };

        _context.FiscalPeriodAuditLogs.Add(auditLog);
        await _context.SaveChangesAsync(cancellationToken);

        return period;
    }

    /// <summary>
    /// Changes a fiscal year's closed status. Reopening from closed to open requires
    /// explicit reason and userId. Logged in FiscalPeriodAuditLog for all child periods.
    /// </summary>
    public async Task<FiscalYear> ReopenFiscalYearAsync(
        int fiscalYearId,
        bool reopen,
        string userId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new InvalidOperationException("User ID is required for fiscal year status changes.");
        }

        var year = await _context.FiscalYears
            .Include(item => item.FiscalPeriods)
            .FirstOrDefaultAsync(item => item.Id == fiscalYearId, cancellationToken)
            ?? throw new InvalidOperationException($"Fiscal year '{fiscalYearId}' was not found.");

        if (year.IsClosed == !reopen)
        {
            return year; // already in target state
        }

        if (reopen)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new InvalidOperationException("An explicit audit reason is required when reopening a closed fiscal year.");
            }
        }

        year.IsClosed = !reopen;

        // Log audit for each period in the year
        foreach (var period in year.FiscalPeriods)
        {
            var previousStatus = period.Status;
            if (reopen && period.Status != FiscalPeriodStatus.Open)
            {
                period.Status = FiscalPeriodStatus.Open;
            }

            var auditLog = new FiscalPeriodAuditLog
            {
                FiscalPeriodId = period.Id,
                FiscalYearId = fiscalYearId,
                UserId = userId,
                Timestamp = DateTimeOffset.UtcNow,
                PreviousStatus = previousStatus,
                NewStatus = period.Status,
                Reason = reopen
                    ? $"Fiscal year '{year.YearName}' reopened. {reason}"
                    : $"Fiscal year '{year.YearName}' closed."
            };

            _context.FiscalPeriodAuditLogs.Add(auditLog);
        }

        await _context.SaveChangesAsync(cancellationToken);
        return year;
    }

    /// <summary>
    /// Retrieves the audit trail for a fiscal period.
    /// </summary>
    public async Task<List<FiscalPeriodAuditLog>> GetAuditTrailAsync(
        Guid fiscalPeriodId,
        CancellationToken cancellationToken = default)
    {
        return await _context.FiscalPeriodAuditLogs
            .AsNoTracking()
            .Where(log => log.FiscalPeriodId == fiscalPeriodId)
            .OrderByDescending(log => log.Timestamp)
            .ToListAsync(cancellationToken);
    }
}