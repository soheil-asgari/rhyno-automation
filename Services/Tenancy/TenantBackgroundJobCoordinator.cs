using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Modules.Platform.Infrastructure.Persistence;
using OfficeAutomation.Models;

namespace OfficeAutomation.Services.Tenancy;

public interface ITenantBackgroundJobCoordinator
{
    Task RunIsolatedAsync(string tenantId, string jobName, Func<IServiceProvider, CancellationToken, Task<bool>> operation, CancellationToken cancellationToken);
}

public sealed class TenantBackgroundJobCoordinator : ITenantBackgroundJobCoordinator
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TenantBackgroundJobCoordinator> _logger;

    public TenantBackgroundJobCoordinator(IServiceScopeFactory scopeFactory, ILogger<TenantBackgroundJobCoordinator> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task RunIsolatedAsync(string tenantId, string jobName, Func<IServiceProvider, CancellationToken, Task<bool>> operation, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        using var tenantScope = scope.ServiceProvider.GetRequiredService<ITenantExecutionScope>().BeginScope(tenantId);
        using var logScope = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("TenantJob").BeginScope(
            new Dictionary<string, object>
            {
                ["TenantId"] = tenantId,
                ["JobName"] = jobName
            });

        var context = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var descriptor = scope.ServiceProvider.GetRequiredService<ITenantIsolationService>().GetCurrent();
        var now = DateTimeOffset.UtcNow;
        var lease = await context.TenantBackgroundJobStates
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.JobName == jobName, cancellationToken);

        if (lease is not null && lease.LockedUntil > now)
        {
            return;
        }

        if (lease is null)
        {
            lease = new TenantBackgroundJobState
            {
                TenantId = tenantId,
                JobName = jobName,
                JobNamespace = descriptor.JobNamespace
            };
            context.TenantBackgroundJobStates.Add(lease);
        }

        lease.JobNamespace = descriptor.JobNamespace;
        lease.LockedBy = Environment.MachineName;
        lease.LockedUntil = now.AddMinutes(2);
        lease.LastStartedAt = now;
        await context.SaveChangesAsync(cancellationToken);

        try
        {
            var completed = await operation(scope.ServiceProvider, cancellationToken);
            lease.LockedUntil = DateTimeOffset.UtcNow;
            lease.LastCompletedAt = completed ? DateTimeOffset.UtcNow : lease.LastCompletedAt;
            lease.LastError = null;
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            lease.LockedUntil = DateTimeOffset.UtcNow;
            lease.LastFailedAt = DateTimeOffset.UtcNow;
            lease.LastError = ex.Message.Length > 1200 ? ex.Message[..1200] : ex.Message;
            await context.SaveChangesAsync(cancellationToken);
            _logger.LogError(ex, "Tenant background job {JobName} failed for tenant {TenantId}.", jobName, tenantId);
            throw;
        }
    }
}
