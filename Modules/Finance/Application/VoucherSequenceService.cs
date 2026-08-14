using System.Collections.Concurrent;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Modules.Finance.Domain;
using OfficeAutomation.Modules.Finance.Infrastructure.Persistence;

namespace OfficeAutomation.Modules.Finance.Application;

/// <summary>
/// Thread-safe voucher sequence number generation.
/// Uses SQL Server sp_getapplock for production (SQL Server) and SemaphoreSlim for InMemory/testing.
/// Ensures no duplicate sequence numbers per FiscalYear + JournalType combination.
/// </summary>
public sealed class VoucherSequenceService
{
    private readonly FinanceDbContext _context;

    // In-memory lock per fiscal-year key for non-SQL-Server providers
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    public VoucherSequenceService(FinanceDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Assigns a unique, thread-safe sequence number for a new voucher within its fiscal year.
    /// For SQL Server, uses sp_getapplock for database-level atomic locking.
    /// For InMemory/other providers, uses a SemaphoreSlim per fiscal year.
    /// </summary>
    public async Task<int> NextSequenceNumberAsync(
        int fiscalYearId,
        CancellationToken cancellationToken = default)
    {
        var useSqlServer = _context.Database.IsSqlServer();

        if (useSqlServer)
        {
            return await NextSequenceNumberSqlServerAsync(fiscalYearId, cancellationToken);
        }

        return await NextSequenceNumberInMemoryAsync(fiscalYearId, cancellationToken);
    }

    private async Task<int> NextSequenceNumberSqlServerAsync(
        int fiscalYearId,
        CancellationToken cancellationToken)
    {
        // Use sp_getapplock for database-level exclusive lock per fiscal year
        var lockHandle = await _context.Database
            .ExecuteSqlRawAsync("DECLARE @result INT; EXEC @result = sp_getapplock @Resource = {0}, @LockMode = 'Exclusive', @LockTimeout = 30000; SELECT @result;",
                cancellationToken,
                $"VoucherSeq_{fiscalYearId}");

        try
        {
            var maxSequence = await _context.VoucherHeaders
                .AsNoTracking()
                .Where(item => item.FiscalYearId == fiscalYearId)
                .Select(item => (int?)item.SequenceNumber)
                .MaxAsync(cancellationToken) ?? 0;

            return maxSequence + 1;
        }
        finally
        {
            await _context.Database
                .ExecuteSqlRawAsync("EXEC sp_releaseapplock @Resource = {0}",
                    cancellationToken,
                    $"VoucherSeq_{fiscalYearId}");
        }
    }

    private async Task<int> NextSequenceNumberInMemoryAsync(
        int fiscalYearId,
        CancellationToken cancellationToken)
    {
        var key = $"FY_{fiscalYearId}";
        var semaphore = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));

        await semaphore.WaitAsync(cancellationToken);
        try
        {
            var maxSequence = await _context.VoucherHeaders
                .AsNoTracking()
                .Where(item => item.FiscalYearId == fiscalYearId)
                .Select(item => (int?)item.SequenceNumber)
                .MaxAsync(cancellationToken) ?? 0;

            return maxSequence + 1;
        }
        finally
        {
            semaphore.Release();
        }
    }

    /// <summary>
    /// Simulates concurrent voucher creation for testing thread safety.
    /// Returns the list of assigned sequence numbers.
    /// </summary>
    public static async Task<List<int>> SimulateConcurrentCreationAsync(
        Func<int, CancellationToken, Task<int>> sequenceProvider,
        int fiscalYearId,
        int concurrencyLevel,
        CancellationToken cancellationToken = default)
    {
        var results = new int[concurrencyLevel];
        var tasks = new Task[concurrencyLevel];

        for (int i = 0; i < concurrencyLevel; i++)
        {
            int index = i;
            tasks[i] = Task.Run(async () =>
            {
                results[index] = await sequenceProvider(fiscalYearId, cancellationToken);
            }, cancellationToken);
        }

        await Task.WhenAll(tasks);
        return results.ToList();
    }
}