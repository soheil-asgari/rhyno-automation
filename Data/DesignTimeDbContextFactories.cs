using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using OfficeAutomation.Modules.Finance.Infrastructure.Persistence;
using OfficeAutomation.Modules.Identity.Infrastructure.Persistence;
using OfficeAutomation.Modules.Inventory.Infrastructure.Persistence;
using OfficeAutomation.Modules.Office.Infrastructure.Persistence;
using OfficeAutomation.Modules.Platform.Infrastructure.Persistence;
using OfficeAutomation.Modules.Workflow.Infrastructure.Persistence;

namespace OfficeAutomation.Data;

public abstract class DesignTimeDbContextFactoryBase<TContext> : IDesignTimeDbContextFactory<TContext>
    where TContext : DbContext
{
    public TContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<TContext>()
            .UseSqlServer(GetConnectionString())
            .Options;

        return Create(options);
    }

    protected abstract TContext Create(DbContextOptions<TContext> options);

    private static string GetConnectionString()
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? "Development";

        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        return configuration.GetConnectionString("PlatformConnection")
            ?? configuration.GetConnectionString("DefaultConnection")
            ?? configuration.GetSection("Tenancy:Tenants:0:ConnectionString").Value
            ?? throw new InvalidOperationException("No design-time database connection string is configured.");
    }
}

public sealed class DesignTimePlatformDbContextFactory : DesignTimeDbContextFactoryBase<PlatformDbContext>
{
    protected override PlatformDbContext Create(DbContextOptions<PlatformDbContext> options)
    {
        return new PlatformDbContext(options);
    }
}

public sealed class DesignTimeWorkflowDbContextFactory : DesignTimeDbContextFactoryBase<WorkflowDbContext>
{
    protected override WorkflowDbContext Create(DbContextOptions<WorkflowDbContext> options)
    {
        return new WorkflowDbContext(options);
    }
}

public sealed class DesignTimeOfficeDbContextFactory : DesignTimeDbContextFactoryBase<OfficeDbContext>
{
    protected override OfficeDbContext Create(DbContextOptions<OfficeDbContext> options)
    {
        return new OfficeDbContext(options);
    }
}

public sealed class DesignTimeInventoryDbContextFactory : DesignTimeDbContextFactoryBase<InventoryDbContext>
{
    protected override InventoryDbContext Create(DbContextOptions<InventoryDbContext> options)
    {
        return new InventoryDbContext(options);
    }
}

public sealed class DesignTimeFinanceDbContextFactory : DesignTimeDbContextFactoryBase<FinanceDbContext>
{
    protected override FinanceDbContext Create(DbContextOptions<FinanceDbContext> options)
    {
        return new FinanceDbContext(options);
    }
}

public sealed class DesignTimeIdentityDbContextFactory : DesignTimeDbContextFactoryBase<IdentityDbContext>
{
    protected override IdentityDbContext Create(DbContextOptions<IdentityDbContext> options)
    {
        return new IdentityDbContext(options);
    }
}
