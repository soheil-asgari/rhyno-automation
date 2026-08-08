using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace OfficeAutomation.Modules.Persistence;

/// <summary>
/// Design-time factory for TaxDbContext. Used by EF Core CLI
/// commands (dotnet ef migrations add / update).
/// </summary>
public sealed class TaxDbContextDesignTimeFactory : IDesignTimeDbContextFactory<TaxDbContext>
{
    public TaxDbContext CreateDbContext(string[] args)
    {
        var basePath = Path.Combine(Directory.GetCurrentDirectory());

        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Server=(localdb)\\mssqllocaldb;Database=OfficeAutoDb;Trusted_Connection=True;MultipleActiveResultSets=true";

        var optionsBuilder = new DbContextOptionsBuilder<TaxDbContext>();
        optionsBuilder.UseSqlServer(connectionString);

        return new TaxDbContext(optionsBuilder.Options, tenantIsolationService: null);
    }
}
