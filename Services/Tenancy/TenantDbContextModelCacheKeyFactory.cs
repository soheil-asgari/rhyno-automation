using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace OfficeAutomation.Services.Tenancy;

/// <summary>
/// A MODEL CACHE KEY FACTORY that deliberately returns the SAME cache key for
/// every tenant, regardless of their database schema.
///
/// WHY THIS IS SAFE:
/// Schema switching is handled entirely at the SQL command level by
/// <see cref="TenantSchemaCommandInterceptor"/>, which rewrites [dbo]. references
/// at execution time. The compiled EF Core model (metadata, navigation fixups,
/// query trees) is identical for all tenants — only the runtime schema name differs.
///
/// THE PREVIOUS PROBLEM:
/// When the key included the tenant's schema name, EF Core compiled and cached a
/// separate model for every single tenant. This caused:
///   1. A massive CPU spike at startup (one full model compile per tenant).
///   2. Unbounded RAM growth — each compiled model is ~10-50 MB and is never evicted.
///   3. Server crashes under load when hundreds of tenants were active.
///
/// THE FIX:
/// Return only (contextType, designTime) — identical to EF Core's built-in default —
/// so the model is compiled exactly ONCE and shared across all tenants.
/// </summary>
public sealed class TenantDbContextModelCacheKeyFactory : IModelCacheKeyFactory
{
    public object Create(DbContext context, bool designTime)
    {
        // Intentionally omit the tenant schema from the cache key.
        // Schema routing is handled by TenantSchemaCommandInterceptor at SQL-execution time.
        return (context.GetType(), designTime);
    }
}
