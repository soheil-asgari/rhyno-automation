using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OfficeAutomation.Modules.Persistence;
using OfficeAutomation.Modules.Persistence.Entities;

namespace OfficeAutomation.Modules.MowadiyanIntegration;

/// <summary>
/// Manages the Mowadiyan submission queue. Enqueues invoices for batch upload and
/// drives the background <see cref="MowadiyanSubmissionQueueHostedService"/>.
/// </summary>
public interface IMowadiyanSubmissionService
{
    /// <summary>Channel reader consumed by the background hosted service.</summary>
    System.Threading.Channels.ChannelReader<Guid> Reader { get; }

    /// <summary>Enqueues all pending (Draft) invoice records for submission.</summary>
    Task<int> EnqueuePendingAsync(CancellationToken cancellationToken);

    /// <summary>Enqueues a single invoice record by id.</summary>
    Task<bool> EnqueueAsync(Guid invoiceRecordId, CancellationToken cancellationToken);

    /// <summary>Retries failed/rejected submissions.</summary>
    Task<int> RetryFailedAsync(CancellationToken cancellationToken);

    /// <summary>Returns the current queue summary counts.</summary>
    Task<MowadiyanQueueSummary> GetSummaryAsync(CancellationToken cancellationToken);
}

/// <summary>Aggregate queue status counts.</summary>
public sealed class MowadiyanQueueSummary
{
    public int Draft { get; set; }
    public int PendingSubmission { get; set; }
    public int Submitted { get; set; }
    public int Success { get; set; }
    public int RejectedByTaxOrg { get; set; }
    public int RetryLimitExceeded { get; set; }
    public int Total => Draft + PendingSubmission + Submitted + Success + RejectedByTaxOrg + RetryLimitExceeded;
    public int InFlight => PendingSubmission + Submitted;
}

/// <inheritdoc />
public sealed class MowadiyanSubmissionService : IMowadiyanSubmissionService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly Channel<Guid> _channel;
    private readonly IOptions<MowadiyanOptions> _options;
    private readonly ILogger<MowadiyanSubmissionService> _logger;

    public MowadiyanSubmissionService(
        IServiceScopeFactory scopeFactory,
        IOptions<MowadiyanOptions> options,
        ILogger<MowadiyanSubmissionService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;

        var capacity = Math.Max(1, options.Value.BatchSize * 4);
        _channel = Channel.CreateBounded<Guid>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });
    }

    public ChannelReader<Guid> Reader => _channel.Reader;

    public async Task<int> EnqueuePendingAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<TaxDbContext>>();
        using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var pendingIds = await db.MowadiyanSubmissions
            .Where(s => s.Status == MowadiyanSubmissionStatus.Draft)
            .Select(s => s.InvoiceRecordId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var enqueued = 0;
        foreach (var id in pendingIds)
        {
            if (await _channel.Writer.WaitToWriteAsync(cancellationToken))
            {
                await _channel.Writer.WriteAsync(id, cancellationToken);
                enqueued++;
            }
        }

        _logger.LogInformation("Enqueued {Count} pending invoice records for Mowadiyan submission", enqueued);
        return enqueued;
    }

    public async Task<bool> EnqueueAsync(Guid invoiceRecordId, CancellationToken cancellationToken)
    {
        await _channel.Writer.WaitToWriteAsync(cancellationToken);
        await _channel.Writer.WriteAsync(invoiceRecordId, cancellationToken);
        _logger.LogInformation("Enqueued invoice {InvoiceId} for Mowadiyan submission", invoiceRecordId);
        return true;
    }

    public async Task<int> RetryFailedAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<TaxDbContext>>();
        using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var failedIds = await db.MowadiyanSubmissions
            .Where(s => s.Status == MowadiyanSubmissionStatus.RejectedByTaxOrg
                        || s.Status == MowadiyanSubmissionStatus.RetryLimitExceeded)
            .Select(s => s.InvoiceRecordId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var enqueued = 0;
        foreach (var id in failedIds)
        {
            if (await RetryMarkAsync(id, db, cancellationToken))
            {
                await _channel.Writer.WaitToWriteAsync(cancellationToken);
                await _channel.Writer.WriteAsync(id, cancellationToken);
                enqueued++;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Retrying {Count} failed Mowadiyan submissions", enqueued);
        return enqueued;
    }
    public async Task<MowadiyanQueueSummary> GetSummaryAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<TaxDbContext>>();
        using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var grouped = await db.MowadiyanSubmissions
            .GroupBy(s => s.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var summary = new MowadiyanQueueSummary();
        foreach (var item in grouped)
        {
            switch (item.Status)
            {
                case MowadiyanSubmissionStatus.Draft: summary.Draft = item.Count; break;
                case MowadiyanSubmissionStatus.PendingSubmission: summary.PendingSubmission = item.Count; break;
                case MowadiyanSubmissionStatus.Submitted: summary.Submitted = item.Count; break;
                case MowadiyanSubmissionStatus.Success: summary.Success = item.Count; break;
                case MowadiyanSubmissionStatus.RejectedByTaxOrg: summary.RejectedByTaxOrg = item.Count; break;
                case MowadiyanSubmissionStatus.RetryLimitExceeded: summary.RetryLimitExceeded = item.Count; break;
            }
        }

        return summary;
    }

    private async Task<bool> RetryMarkAsync(
        Guid invoiceRecordId, TaxDbContext db, CancellationToken cancellationToken)
    {
        var submission = await db.MowadiyanSubmissions
            .Where(s => s.InvoiceRecordId == invoiceRecordId)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (submission is null)
        {
            return false;
        }

        submission.Status = MowadiyanSubmissionStatus.PendingSubmission;
        submission.StatusText = "PendingSubmission";
        submission.RetryCount = 0;
        submission.NextRetryAtUtc = null;
        submission.UpdatedAt = DateTime.Now;
        return true;
    }
}