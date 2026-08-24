using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OfficeAutomation.Services.Contracts;

namespace OfficeAutomation.Services;

/// <summary>
/// BackgroundService برای شناسایی دوره‌ای درآمد قراردادها (Accrual Revenue Recognition).
/// هر روز اجرا می‌شود و برای قراردادهای فعال، بخشی از درآمد معوق را به عنوان درآمد قطعی
/// شناسایی می‌کند.
/// </summary>
public sealed class ContractRevenueRecognitionBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ContractRevenueRecognitionBackgroundService> _logger;

    public ContractRevenueRecognitionBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<ContractRevenueRecognitionBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = DateTime.UtcNow;

                // Run at 02:00 AM daily
                if (now.Hour == 2)
                {
                    using var scope = _scopeFactory.CreateScope();
                    var accountingService = scope.ServiceProvider.GetRequiredService<IContractAccountingService>();

                    // Use a system user ID for automated processing
                    const string systemUserId = "SYSTEM";

                    var updated = await accountingService.RecognizeAllActiveContractsAsync(now, systemUserId, stoppingToken);
                    if (updated > 0)
                    {
                        _logger.LogInformation("Daily revenue recognition completed: {Count} contracts processed.", updated);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in revenue recognition background service.");
            }

            // Check every hour
            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }
}