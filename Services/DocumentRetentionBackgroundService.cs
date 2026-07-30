using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Models;
using OfficeAutomation.Modules.Workflow.Infrastructure.Persistence;
using OfficeAutomation.Services.Tenancy;

namespace OfficeAutomation.Services;

/// <summary>
/// BackgroundService برای بررسی و آرشیو خودکار اسناد منقضی‌شده
/// </summary>
public sealed class DocumentRetentionBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ITenantRegistry _tenantRegistry;
    private readonly ILogger<DocumentRetentionBackgroundService> _logger;

    public DocumentRetentionBackgroundService(
        IServiceScopeFactory scopeFactory,
        ITenantRegistry tenantRegistry,
        ILogger<DocumentRetentionBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _tenantRegistry = tenantRegistry;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var tenants = await _tenantRegistry.GetAllTenantsAsync(stoppingToken);
                foreach (var tenant in tenants)
                {
                    using var scope = _scopeFactory.CreateScope();
                    var tenantExecutionScope = scope.ServiceProvider.GetRequiredService<ITenantExecutionScope>();
                    using var _ = tenantExecutionScope.BeginScope(tenant.TenantId);

                    var context = scope.ServiceProvider.GetRequiredService<IWorkflowDbContext>();
                    var now = DateTime.Now;

                    var expiredDocs = await context.DocumentArchiveItems
                        .Where(d => d.ExpirationDate.HasValue
                                 && d.ExpirationDate.Value <= now
                                 && !d.IsArchived)
                        .ToListAsync(stoppingToken);

                    foreach (var doc in expiredDocs)
                    {
                        doc.IsArchived = true;
                        _logger.LogInformation(
                            "Document {DocumentId} '{Title}' archived due to expiration date {ExpirationDate}",
                            doc.Id, doc.Title, doc.ExpirationDate);
                    }

                    if (expiredDocs.Count > 0)
                    {
                        await context.SaveChangesAsync(stoppingToken);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while processing document retention policy.");
            }

            // Check every hour
            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }
}