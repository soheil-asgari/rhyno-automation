using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OfficeAutomation.Services.Contracts;

namespace OfficeAutomation.Services;

/// <summary>
/// Hangfire job methods for contract background processing.
/// Each method is invoked by Hangfire's recurring job scheduler with persisted
/// execution, automatic retry, and distributed locking — replacing the old
/// BackgroundService loop that could miss jobs across app pool restarts.
/// </summary>
public sealed class ContractHangfireJobs
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ContractHangfireJobs> _logger;

    public ContractHangfireJobs(
        IServiceScopeFactory scopeFactory,
        ILogger<ContractHangfireJobs> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Processes auto-renewals for all qualifying contracts.
    /// Scheduled daily at 04:00 UTC via Hangfire recurring job.
    /// </summary>
    [DisableConcurrentExecution(timeoutInSeconds: 600)]
    [AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 60, 300, 900 })]
    [Queue("contracts")]
    public async Task ProcessAutoRenewalsAsync(CancellationToken ct)
    {
        _logger.LogInformation("[Hangfire] Starting auto-renewal processing...");
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var contractService = scope.ServiceProvider.GetRequiredService<IContractService>();
            var count = await contractService.ProcessAutoRenewalsAsync(ct);
            _logger.LogInformation("[Hangfire] Auto-renewal completed: {Count} contracts renewed.", count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Hangfire] Auto-renewal processing failed.");
            throw; // Re-throw so Hangfire marks the job as failed and retries
        }
    }

    /// <summary>
    /// Checks contract expiry dates and sends notifications.
    /// Scheduled daily at 01:00 UTC.
    /// </summary>
    [DisableConcurrentExecution(timeoutInSeconds: 600)]
    [AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 60, 300, 900 })]
    [Queue("contracts")]
    public async Task ProcessExpiryCheckAsync(CancellationToken ct)
    {
        _logger.LogInformation("[Hangfire] Starting expiry check...");
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var coordinator = scope.ServiceProvider.GetRequiredService<ContractBackgroundJobCoordinator>();
            await coordinator.RunExpiryCheckPublicAsync(ct);
            _logger.LogInformation("[Hangfire] Expiry check completed.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Hangfire] Expiry check failed.");
            throw;
        }
    }

    /// <summary>
    /// Checks for overdue payments and sends notifications.
    /// Scheduled daily at 01:30 UTC.
    /// </summary>
    [DisableConcurrentExecution(timeoutInSeconds: 600)]
    [AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 60, 300, 900 })]
    [Queue("contracts")]
    public async Task ProcessOverduePaymentCheckAsync(CancellationToken ct)
    {
        _logger.LogInformation("[Hangfire] Starting overdue payment check...");
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var coordinator = scope.ServiceProvider.GetRequiredService<ContractBackgroundJobCoordinator>();
            await coordinator.RunOverduePaymentCheckPublicAsync(ct);
            _logger.LogInformation("[Hangfire] Overdue payment check completed.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Hangfire] Overdue payment check failed.");
            throw;
        }
    }

    /// <summary>
    /// Runs revenue recognition for all active contracts.
    /// Scheduled daily at 02:00 UTC.
    /// </summary>
    [DisableConcurrentExecution(timeoutInSeconds: 600)]
    [AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 60, 300, 900 })]
    [Queue("contracts")]
    public async Task ProcessRevenueRecognitionAsync(CancellationToken ct)
    {
        _logger.LogInformation("[Hangfire] Starting revenue recognition...");
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var accountingService = scope.ServiceProvider.GetRequiredService<IContractAccountingService>();
            var updated = await accountingService.RecognizeAllActiveContractsAsync(DateTime.UtcNow, "SYSTEM", ct);
            _logger.LogInformation("[Hangfire] Revenue recognition completed: {Count} contracts processed.", updated);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Hangfire] Revenue recognition failed.");
            throw;
        }
    }

    /// <summary>
    /// Calculates and applies penalties for overdue payments.
    /// Scheduled daily at 03:00 UTC.
    /// </summary>
    [DisableConcurrentExecution(timeoutInSeconds: 600)]
    [AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 60, 300, 900 })]
    [Queue("contracts")]
    public async Task ProcessPenaltyCalculationAsync(CancellationToken ct)
    {
        _logger.LogInformation("[Hangfire] Starting penalty calculation...");
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var penaltyService = scope.ServiceProvider.GetRequiredService<IPaymentPenaltyService>();
            var count = await penaltyService.ApplyCalculatedPenaltiesAsync(ct);
            _logger.LogInformation("[Hangfire] Penalty calculation completed: {Count} penalties processed.", count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Hangfire] Penalty calculation failed.");
            throw;
        }
    }

    /// <summary>
    /// Registers recurring Hangfire jobs for contracts processing with persistent cron schedules.
    /// </summary>
    public static void RegisterRecurringJobs()
    {
        // 1. Contract Expiry Check - Daily at 01:00 UTC
        RecurringJob.AddOrUpdate<ContractHangfireJobs>(
            "contract-expiry-check",
            job => job.ProcessExpiryCheckAsync(CancellationToken.None),
            "0 1 * * *",
            new RecurringJobOptions { QueueName = "contracts" });

        // 2. Overdue Payment Check - Daily at 01:30 UTC
        RecurringJob.AddOrUpdate<ContractHangfireJobs>(
            "contract-overdue-payments",
            job => job.ProcessOverduePaymentCheckAsync(CancellationToken.None),
            "30 1 * * *",
            new RecurringJobOptions { QueueName = "contracts" });

        // 3. Revenue Recognition - Daily at 02:00 UTC
        RecurringJob.AddOrUpdate<ContractHangfireJobs>(
            "contract-revenue-recognition",
            job => job.ProcessRevenueRecognitionAsync(CancellationToken.None),
            "0 2 * * *",
            new RecurringJobOptions { QueueName = "contracts" });

        // 4. Overdue Penalty Calculation - Daily at 03:00 UTC
        RecurringJob.AddOrUpdate<ContractHangfireJobs>(
            "contract-penalty-calculation",
            job => job.ProcessPenaltyCalculationAsync(CancellationToken.None),
            "0 3 * * *",
            new RecurringJobOptions { QueueName = "contracts" });

        // 5. Contract Auto-Renewal - Daily at 04:00 UTC (Resilient scheduler)
        RecurringJob.AddOrUpdate<ContractHangfireJobs>(
            "contract-auto-renewals",
            job => job.ProcessAutoRenewalsAsync(CancellationToken.None),
            "0 4 * * *",
            new RecurringJobOptions { QueueName = "contracts" });
    }
}
