using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using OfficeAutomation.Data;
using OfficeAutomation.Services.Tenancy;

namespace OfficeAutomation.Modules.Persistence;

/// <summary>
/// Scoped factory for <see cref="TaxDbContext"/>. Registered as a scoped service so
/// EF Core builds the <see cref="DbContextOptions{TaxDbContext}"/> from the CURRENT
/// scope's <see cref="IServiceProvider"/> instead of the root provider.
///
/// This is the critical fix for the *
/// "Cannot resolve scoped service 'ITenantConnectionStringResolver' from root provider" *
/// exception. The previous <c>AddDbContextFactory&lt;TaxDbContext&gt;(ConfigureTenantDbContext)</c>
/// registration resolved the options delegate with the ROOT provider, which is illegal for the
/// scoped tenancy services (<see cref="ITenantConnectionStringResolver"/>, the EF interceptors, etc.).
/// </summary>
public sealed class TaxDbContextFactory : IDbContextFactory<TaxDbContext>
{
    private readonly DbContextOptions<TaxDbContext> _options;

    public TaxDbContextFactory(IServiceProvider serviceProvider)
    {
        _options = new DbContextOptionsBuilder<TaxDbContext>()
            .UseSqlServer(
                serviceProvider
                    .GetRequiredService<ITenantConnectionStringResolver>()
                    .GetConnectionString())
            .ConfigureWarnings(warnings =>
                warnings.Ignore(RelationalEventId.PendingModelChangesWarning))
            .ReplaceService<IModelCacheKeyFactory, TenantDbContextModelCacheKeyFactory>()
            .AddInterceptors(
                serviceProvider.GetRequiredService<AuditSaveChangesInterceptor>(),
                serviceProvider.GetRequiredService<RowLevelSecuritySaveChangesInterceptor>(),
                serviceProvider.GetRequiredService<TenantSchemaCommandInterceptor>())
            .Options;
    }

    public TaxDbContext CreateDbContext()
        => new(_options);

    public Task<TaxDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new TaxDbContext(_options));
}