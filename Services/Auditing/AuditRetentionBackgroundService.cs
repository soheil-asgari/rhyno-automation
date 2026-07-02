using Microsoft.Extensions.Options;
using OfficeAutomation.Services.Tenancy;

namespace OfficeAutomation.Services.Auditing;

public sealed class AuditRetentionBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ITenantRegistry _tenantRegistry;
    private readonly AuditRetentionOptions _options;
    private readonly ILogger<AuditRetentionBackgroundService> _logger;

    public AuditRetentionBackgroundService(
        IServiceScopeFactory scopeFactory,
        ITenantRegistry tenantRegistry,
        IOptions<AuditRetentionOptions> options,
        ILogger<AuditRetentionBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _tenantRegistry = tenantRegistry;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                foreach (var tenant in _tenantRegistry.GetAllTenants())
                {
                    using var scope = _scopeFactory.CreateScope();
                    using var tenantScope = scope.ServiceProvider.GetRequiredService<ITenantExecutionScope>().BeginScope(tenant.TenantId);
                    await scope.ServiceProvider.GetRequiredService<AuditRetentionService>().ArchiveExpiredAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while archiving audit logs.");
            }

            await Task.Delay(TimeSpan.FromHours(_options.Enabled ? 24 : 1), stoppingToken);
        }
    }
}
