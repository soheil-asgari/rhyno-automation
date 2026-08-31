using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace OfficeAutomation;

public static class DatabaseExtensions
{
    public static void ApplyPendingMigrations(this IHost app)
    {
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;

        var dbContextTypes = new[]
        {
            typeof(Modules.Identity.Infrastructure.Persistence.IdentityDbContext),
            typeof(Modules.Platform.Infrastructure.Persistence.PlatformDbContext),
            typeof(Modules.Office.Infrastructure.Persistence.OfficeDbContext),
            typeof(Modules.Finance.Infrastructure.Persistence.FinanceDbContext),
            typeof(Modules.Inventory.Infrastructure.Persistence.InventoryDbContext),
            typeof(Modules.Workflow.Infrastructure.Persistence.WorkflowDbContext),
            typeof(Modules.Persistence.TaxDbContext)
        };

        foreach (var type in dbContextTypes)
        {
            if (services.GetService(type) is DbContext dbContext)
            {
                dbContext.Database.Migrate();
            }
        }
    }
}