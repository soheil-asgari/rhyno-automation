using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OfficeAutomation.Modules.Finance.Infrastructure.Persistence;
using OfficeAutomation.Modules.Identity.Infrastructure.Persistence;
using OfficeAutomation.Modules.Inventory.Infrastructure.Persistence;
using OfficeAutomation.Modules.Office.Infrastructure.Persistence;
using OfficeAutomation.Modules.Platform.Infrastructure.Persistence;
using OfficeAutomation.Modules.Workflow.Infrastructure.Persistence;
using OfficeAutomation.Modules.Persistence;

namespace OfficeAutomation.Services;

/// <summary>
/// Applies pending EF Core migrations for every DbContext in the application
/// on startup. Uses a strict fail-fast strategy: if any context fails to
/// migrate, the exception is re-thrown and the application terminates.
/// No <c>EnsureCreated</c> fallback is used — that anti-pattern bypasses
/// the <c>__EFMigrationsHistory</c> table and breaks future migrations.
/// </summary>
public static class DatabaseMigrationService
{
    /// <summary>
    /// Migrates all 7 DbContexts sequentially. Each context is resolved from
    /// <paramref name="serviceProvider"/> and migrated via
    /// <c>Database.MigrateAsync()</c>. If any migration fails, the exception
    /// is logged and re-thrown immediately (fail-fast).
    /// </summary>
    /// <param name="serviceProvider">The application's root service provider.</param>
    /// <param name="logger">An <see cref="ILogger"/> for progress logging.</param>
    /// <exception cref="Exception">Re-throws the inner migration exception unchanged.</exception>
    public static async Task MigrateAllAsync(IServiceProvider serviceProvider, ILogger logger)
    {
        // ── PlatformDbContext (shared/control plane) ──
        await MigrateContextAsync<PlatformDbContext>(serviceProvider, logger, "PlatformDbContext");

        // ── IdentityDbContext (ASP.NET Identity / users, roles) ──
        await MigrateContextAsync<IdentityDbContext>(serviceProvider, logger, "IdentityDbContext");

        // ── FinanceDbContext (financial ledgers, vouchers, etc.) ──
        await MigrateContextAsync<FinanceDbContext>(serviceProvider, logger, "FinanceDbContext");

        // ── InventoryDbContext (warehouse, inventory) ──
        await MigrateContextAsync<InventoryDbContext>(serviceProvider, logger, "InventoryDbContext");

        // ── WorkflowDbContext (workflow definitions, instances) ──
        await MigrateContextAsync<WorkflowDbContext>(serviceProvider, logger, "WorkflowDbContext");

        // ── OfficeDbContext (contracts, customers, templates) ──
        await MigrateContextAsync<OfficeDbContext>(serviceProvider, logger, "OfficeDbContext");

        // ── TaxDbContext (invoice records, tax submissions) ──
        // TaxDbContext is resolved via IDbContextFactory<TaxDbContext>
        await MigrateTaxDbContextAsync(serviceProvider, logger);

        logger.LogInformation("All 7 database context migrations applied successfully on startup.");
    }

    /// <summary>
    /// Generic helper that resolves <typeparamref name="TContext"/> from a
    /// new DI scope, applies pending migrations, and fails fast on error.
    /// </summary>
    /// <typeparam name="TContext">
    /// A standard <see cref="DbContext"/> registered directly in the DI container.
    /// </typeparam>
    private static async Task MigrateContextAsync<TContext>(
        IServiceProvider serviceProvider,
        ILogger logger,
        string contextName)
        where TContext : DbContext
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();

        logger.LogInformation("Migrating {ContextName}...", contextName);
        await context.Database.MigrateAsync();
        logger.LogInformation("{ContextName} migrations applied successfully on startup.", contextName);
    }

    /// <summary>
    /// Migrates <see cref="TaxDbContext"/> which is registered via
    /// <c>IDbContextFactory&lt;TaxDbContext&gt;</c> and must be resolved
    /// through the factory rather than directly from DI.
    /// </summary>
    private static async Task MigrateTaxDbContextAsync(
        IServiceProvider serviceProvider,
        ILogger logger)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<TaxDbContext>>();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();

        logger.LogInformation("Migrating TaxDbContext...");
        await dbContext.Database.MigrateAsync();
        logger.LogInformation("TaxDbContext migrations applied successfully on startup.");
    }
}