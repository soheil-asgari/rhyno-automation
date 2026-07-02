using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OfficeAutomation.Models;
using OfficeAutomation.Modules.Workflow.Infrastructure.Persistence;

namespace OfficeAutomation.Services.Outbox;

public sealed class OutboxPublisherBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxPublisherBackgroundService> _logger;
    private readonly OutboxOptions _options;
    private readonly Services.Tenancy.ITenantRegistry _tenantRegistry;

    public OutboxPublisherBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptions<OutboxOptions> options,
        ILogger<OutboxPublisherBackgroundService> logger,
        Services.Tenancy.ITenantRegistry tenantRegistry)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options.Value;
        _tenantRegistry = tenantRegistry;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var tenants = await _tenantRegistry.GetAllTenantsAsync(stoppingToken);
                foreach (var tenant in tenants.Where(item => item.EnableOutboxPublisher))
                {
                    using var scope = _scopeFactory.CreateScope();
                    var tenantBackgroundJobCoordinator = scope.ServiceProvider.GetRequiredService<Services.Tenancy.ITenantBackgroundJobCoordinator>();
                    await tenantBackgroundJobCoordinator.RunIsolatedAsync(tenant.TenantId, "outbox-publisher", async (_, cancellationToken) =>
                    {
                        await PublishBatchAsync(tenant.TenantId, cancellationToken);
                        return true;
                    }, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while publishing outbox messages.");
            }

            await Task.Delay(TimeSpan.FromSeconds(Math.Max(1, _options.PollIntervalSeconds)), stoppingToken);
        }
    }

    private async Task PublishBatchAsync(string tenantId, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        scope.ServiceProvider.GetRequiredService<Services.Tenancy.ITenantExecutionScope>().BeginScope(tenantId);
        var context = scope.ServiceProvider.GetRequiredService<WorkflowDbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IEventBusPublisher>();
        var now = DateTimeOffset.UtcNow;
        var lockUntil = now.AddSeconds(Math.Max(10, _options.LockTimeoutSeconds));

        var messages = await context.OutboxMessages
            .Where(item =>
                item.TenantId == tenantId &&
                (item.Status == OutboxMessageStatus.Pending || item.Status == OutboxMessageStatus.Failed) &&
                (item.LockedUntil == null || item.LockedUntil < now))
            .OrderBy(item => item.Id)
            .Take(Math.Max(1, _options.BatchSize))
            .ToListAsync(cancellationToken);

        if (messages.Count == 0)
        {
            return;
        }

        foreach (var message in messages)
        {
            message.Status = OutboxMessageStatus.Processing;
            message.LockedUntil = lockUntil;
            message.LastAttemptAt = now;
        }

        await context.SaveChangesAsync(cancellationToken);

        foreach (var message in messages)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(message.MessageId))
                {
                    message.MessageId = $"outbox-{message.Id}";
                }

                await publisher.PublishAsync(message.ExchangeName, message.RoutingKey, message.PayloadJson, message.MessageId, cancellationToken);
                message.Status = OutboxMessageStatus.Processed;
                message.ProcessedAt = DateTimeOffset.UtcNow;
                message.LockedUntil = null;
                message.LastError = null;
            }
            catch (Exception ex)
            {
                message.RetryCount++;
                message.Status = message.RetryCount >= _options.MaxRetryCount
                    ? OutboxMessageStatus.DeadLetter
                    : OutboxMessageStatus.Pending;
                message.LockedUntil = null;
                message.LastError = ex.Message.Length > 1200 ? ex.Message[..1200] : ex.Message;
                _logger.LogWarning(ex, "Failed to publish outbox message {OutboxMessageId} ({MessageId}). Status={Status} RetryCount={RetryCount}.", message.Id, message.MessageId, message.Status, message.RetryCount);
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
