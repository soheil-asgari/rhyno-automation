using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace OfficeAutomation.Services.Tenancy;

public sealed class TenantDbContextModelCacheKeyFactory : IModelCacheKeyFactory
{
    public object Create(DbContext context, bool designTime)
    {
        if (context is not Data.ITenantSchemaDbContext tenantSchemaDbContext)
        {
            return (context.GetType(), designTime);
        }

        return (context.GetType(), tenantSchemaDbContext.CurrentDatabaseSchema ?? "dbo", designTime);
    }
}
