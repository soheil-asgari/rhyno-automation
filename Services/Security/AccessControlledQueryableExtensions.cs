using Microsoft.EntityFrameworkCore;

namespace OfficeAutomation.Services.Security;

public static class AccessControlledQueryableExtensions
{
    public static IQueryable<T> ApplyDepartmentScope<T>(
        this IQueryable<T> query,
        ICurrentDataAccessScope scope)
        where T : class
    {
        if (!scope.IsInitialized || scope.HasGlobalAccess)
        {
            return query;
        }

        if (!scope.IsAuthenticated || !scope.DepartmentId.HasValue)
        {
            return query.Where(_ => false);
        }

        var entityType = typeof(T);
        var property = entityType.GetProperty("DepartmentId");
        if (property == null || property.PropertyType != typeof(int) && property.PropertyType != typeof(int?))
        {
            return query;
        }

        return query.Where(item => EF.Property<int?>(item, "DepartmentId") == scope.DepartmentId);
    }

    public static IQueryable<T> ApplyTenantScope<T>(
        this IQueryable<T> query,
        ICurrentDataAccessScope scope)
        where T : class
    {
        if (string.IsNullOrWhiteSpace(scope.TenantId))
        {
            return query;
        }

        var property = typeof(T).GetProperty("TenantId");
        if (property == null || property.PropertyType != typeof(string))
        {
            return query;
        }

        return query.Where(item => EF.Property<string?>(item, "TenantId") == scope.TenantId);
    }

    public static IQueryable<T> ApplyCurrentAccessScope<T>(
        this IQueryable<T> query,
        ICurrentDataAccessScope scope)
        where T : class
    {
        return query
            .ApplyTenantScope(scope)
            .ApplyDepartmentScope(scope);
    }
}
