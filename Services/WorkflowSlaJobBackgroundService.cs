using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Data;
using OfficeAutomation.Models;

namespace OfficeAutomation.Services;

public sealed class WorkflowSlaJobBackgroundService : BackgroundService
{
    private readonly ILogger<WorkflowSlaJobBackgroundService> _logger;
    private readonly Tenancy.ITenantRegistry _tenantRegistry;
    private readonly IServiceScopeFactory _scopeFactory;

    public WorkflowSlaJobBackgroundService(
        ILogger<WorkflowSlaJobBackgroundService> logger,
        Tenancy.ITenantRegistry tenantRegistry,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _tenantRegistry = tenantRegistry;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var processedAny = false;
                var tenants = await _tenantRegistry.GetAllTenantsAsync(stoppingToken);
                foreach (var tenant in tenants.Where(item => item.EnableWorkflowJobs))
                {
                    var jobProcessed = false;
                    using var scope = _scopeFactory.CreateScope();
                    var tenantBackgroundJobCoordinator = scope.ServiceProvider.GetRequiredService<Tenancy.ITenantBackgroundJobCoordinator>();
                    await tenantBackgroundJobCoordinator.RunIsolatedAsync(tenant.TenantId, "workflow-sla", async (serviceProvider, cancellationToken) =>
                    {
                        var context = serviceProvider.GetRequiredService<OfficeAutomation.Modules.Workflow.Infrastructure.Persistence.WorkflowDbContext>();
                        var notifier = serviceProvider.GetRequiredService<WorkflowSlaEscalationNotifier>();

                        var nextJob = await context.WorkflowSlaJobs
                            .Where(item => item.Status == WorkflowSlaJobStatus.Scheduled)
                            .OrderBy(item => item.ScheduledFor)
                            .FirstOrDefaultAsync(cancellationToken);

                        if (nextJob == null || nextJob.ScheduledFor > DateTimeOffset.UtcNow)
                        {
                            return false;
                        }

                        var trackedJob = await context.WorkflowSlaJobs.FirstOrDefaultAsync(item => item.Id == nextJob.Id, cancellationToken);
                        if (trackedJob == null || trackedJob.Status != WorkflowSlaJobStatus.Scheduled)
                        {
                            return false;
                        }

                        await notifier.EscalateStepAsync(trackedJob.WorkflowStepId, cancellationToken);
                        trackedJob.Status = WorkflowSlaJobStatus.Completed;
                        trackedJob.CompletedAt = DateTimeOffset.UtcNow;
                        await context.SaveChangesAsync(cancellationToken);
                        jobProcessed = true;
                        return true;
                    }, stoppingToken);
                    processedAny |= jobProcessed;
                }

                if (!processedAny)
                {
                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while processing workflow SLA jobs.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}
