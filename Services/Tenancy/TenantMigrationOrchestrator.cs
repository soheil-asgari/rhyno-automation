using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using OfficeAutomation.Models;
using OfficeAutomation.Modules.Finance.Infrastructure.Persistence;
using OfficeAutomation.Modules.Inventory.Infrastructure.Persistence;
using OfficeAutomation.Modules.Office.Infrastructure.Persistence;
using OfficeAutomation.Modules.Platform.Infrastructure.Persistence;
using OfficeAutomation.Modules.Workflow.Infrastructure.Persistence;

namespace OfficeAutomation.Services.Tenancy;

public sealed class TenantMigrationOrchestrator
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> TenantLocks = new(StringComparer.OrdinalIgnoreCase);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TenantOptions _tenantOptions;
    private readonly ILogger<TenantMigrationOrchestrator> _logger;

    public TenantMigrationOrchestrator(
        IServiceScopeFactory scopeFactory,
        IOptions<TenantOptions> tenantOptions,
        ILogger<TenantMigrationOrchestrator> logger)
    {
        _scopeFactory = scopeFactory;
        _tenantOptions = tenantOptions.Value;
        _logger = logger;
    }

    public async Task MigrateActiveTenantsAsync(CancellationToken cancellationToken = default)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var platformContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();

        _logger.LogInformation("Tenant migration: migrating platform catalog.");
        await platformContext.Database.MigrateAsync(cancellationToken);
        await SeedConfiguredTenantsAsync(platformContext, cancellationToken);

        var tenants = await platformContext.TenantDefinitions
            .AsNoTracking()
            .Where(item => item.LifecycleState == TenantLifecycleState.Active)
            .OrderBy(item => item.TenantId)
            .ToListAsync(cancellationToken);

        foreach (var tenant in tenants)
        {
            await MigrateTenantAsync(tenant, cancellationToken);
        }
    }

    private async Task SeedConfiguredTenantsAsync(PlatformDbContext context, CancellationToken cancellationToken)
    {
        foreach (var configuredTenant in _tenantOptions.Tenants)
        {
            var exists = await context.TenantDefinitions
                .AnyAsync(item => item.TenantId == configuredTenant.TenantId, cancellationToken);
            if (exists)
            {
                continue;
            }

            NormalizeTenant(configuredTenant);
            context.TenantDefinitions.Add(configuredTenant);
        }

        if (context.ChangeTracker.HasChanges())
        {
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task MigrateTenantAsync(TenantDefinition tenant, CancellationToken cancellationToken)
    {
        var key = $"{tenant.TenantId}:{tenant.ConnectionString}";
        var tenantLock = TenantLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));

        await tenantLock.WaitAsync(cancellationToken);
        try
        {
            _logger.LogInformation("Tenant migration: migrating tenant {TenantId}.", tenant.TenantId);

            await using var platformContext = CreateDbContext<PlatformDbContext>(tenant.ConnectionString);
            await platformContext.Database.MigrateAsync(cancellationToken);

            await using var financeContext = CreateDbContext<FinanceDbContext>(tenant.ConnectionString);
            await financeContext.Database.MigrateAsync(cancellationToken);

            await using var workflowContext = CreateDbContext<WorkflowDbContext>(tenant.ConnectionString);
            await workflowContext.Database.MigrateAsync(cancellationToken);

            await using var inventoryContext = CreateDbContext<InventoryDbContext>(tenant.ConnectionString);
            await inventoryContext.Database.MigrateAsync(cancellationToken);

            await using var officeContext = CreateDbContext<OfficeDbContext>(tenant.ConnectionString);
            await officeContext.Database.MigrateAsync(cancellationToken);
        }
        finally
        {
            tenantLock.Release();
        }
    }

    private static TContext CreateDbContext<TContext>(string connectionString)
        where TContext : DbContext
    {
        var options = new DbContextOptionsBuilder<TContext>()
            .UseSqlServer(connectionString)
            .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options;

        return (TContext)Activator.CreateInstance(typeof(TContext), options, null)!;
    }

    private static void NormalizeTenant(TenantDefinition tenant)
    {
        tenant.LifecycleState = string.IsNullOrWhiteSpace(tenant.LifecycleState) ? TenantLifecycleState.Active : tenant.LifecycleState.Trim();
        tenant.Plan = string.IsNullOrWhiteSpace(tenant.Plan) ? "Standard" : tenant.Plan.Trim();
        tenant.DatabaseSchema = string.IsNullOrWhiteSpace(tenant.DatabaseSchema) ? "dbo" : tenant.DatabaseSchema.Trim();
        tenant.QueueNamespace = string.IsNullOrWhiteSpace(tenant.QueueNamespace) ? tenant.TenantId.ToLowerInvariant() : tenant.QueueNamespace.Trim();
        tenant.CachePrefix = string.IsNullOrWhiteSpace(tenant.CachePrefix) ? tenant.TenantId.ToLowerInvariant() : tenant.CachePrefix.Trim();
        tenant.StorageRoot = string.IsNullOrWhiteSpace(tenant.StorageRoot) ? Path.Combine("tenants", tenant.TenantId.ToLowerInvariant()) : tenant.StorageRoot.Trim();
        tenant.LogPrefix = string.IsNullOrWhiteSpace(tenant.LogPrefix) ? tenant.TenantId.ToLowerInvariant() : tenant.LogPrefix.Trim();
        tenant.LogRoot = string.IsNullOrWhiteSpace(tenant.LogRoot) ? Path.Combine("logs", tenant.LogPrefix) : tenant.LogRoot.Trim();
        tenant.SettingsNamespace = string.IsNullOrWhiteSpace(tenant.SettingsNamespace) ? $"{tenant.TenantId.ToLowerInvariant()}:settings" : tenant.SettingsNamespace.Trim();
        tenant.JobNamespace = string.IsNullOrWhiteSpace(tenant.JobNamespace) ? $"{tenant.TenantId.ToLowerInvariant()}:jobs" : tenant.JobNamespace.Trim();
    }
}

public sealed class TenantMigrationHostedService : IHostedService
{
    private static readonly TimeSpan DefaultStartupMigrationTimeout = TimeSpan.FromMinutes(5);

    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<TenantMigrationHostedService> _logger;

    public TenantMigrationHostedService(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        IHostEnvironment environment,
        ILogger<TenantMigrationHostedService> logger)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _environment = environment;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_configuration.GetValue("Database:ApplyMigrationsOnStartup", _environment.IsDevelopment()))
        {
            _logger.LogInformation("Tenant migration hosted service skipped because Database:ApplyMigrationsOnStartup is disabled.");
            return;
        }

        if (!_environment.IsDevelopment() &&
            !_configuration.GetValue("Database:ApplyMigrationsOnStartup", false))
        {
            _logger.LogInformation("Tenant migration hosted service skipped outside Development.");
            return;
        }

        var timeout = _configuration.GetValue<TimeSpan?>("Database:StartupMigrationTimeout")
            ?? DefaultStartupMigrationTimeout;

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        try
        {
            await using var scope = _serviceProvider.CreateAsyncScope();
            await scope.ServiceProvider
                .GetRequiredService<TenantMigrationOrchestrator>()
                .MigrateActiveTenantsAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && timeoutCts.IsCancellationRequested)
        {
            _logger.LogError(
                "Tenant migration did not finish within {Timeout}. Check SQL Server availability and connection strings.",
                timeout);
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
