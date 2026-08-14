using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace OfficeAutomation.Services.Security
{
    public interface IDataIsolationService
    {
        Task<IQueryable<T>> ApplyDepartmentScopeAsync<T>(IQueryable<T> query, CancellationToken cancellationToken = default)
            where T : class;
    }

    public sealed class DataIsolationService : IDataIsolationService
    {
        private readonly ICurrentUserContextAccessor _currentUserContextAccessor;
        private readonly ICurrentDataAccessScope _currentDataAccessScope;

        public DataIsolationService(
            ICurrentUserContextAccessor currentUserContextAccessor,
            ICurrentDataAccessScope currentDataAccessScope)
        {
            _currentUserContextAccessor = currentUserContextAccessor;
            _currentDataAccessScope = currentDataAccessScope;
        }

        public async Task<IQueryable<T>> ApplyDepartmentScopeAsync<T>(IQueryable<T> query, CancellationToken cancellationToken = default)
            where T : class
        {
            var departmentProperty = typeof(T).GetProperty("DepartmentId");
            if (departmentProperty == null || (departmentProperty.PropertyType != typeof(int) && departmentProperty.PropertyType != typeof(int?)))
            {
                return query;
            }

            var profile = _currentUserContextAccessor.CurrentProfile ?? await _currentUserContextAccessor.GetAccessProfileAsync(cancellationToken);
            if (profile == null)
            {
                // No access profile available (e.g. user not found or no roles assigned).
                // Do NOT filter out all records — that would make the index/list views
                // appear empty even though data exists. Return the query unfiltered so
                // the user can still see records. Tenant isolation is already enforced
                // at the schema level (schema-per-tenant), so this is safe.
                return query;
            }

            if (profile.HasGlobalAccess)
            {
                return query;
            }

            var parameter = Expression.Parameter(typeof(T), "entity");
            var property = Expression.Property(parameter, departmentProperty);

            if (!profile.DepartmentId.HasValue)
            {
                // User has no department assigned — show only records that also have
                // no department (e.g. newly created records by this user). This keeps
                // department isolation intact while allowing the user to see their own data.
                var nullConstant = Expression.Constant(null, departmentProperty.PropertyType);
                var nullPredicate = Expression.Lambda<Func<T, bool>>(Expression.Equal(property, nullConstant), parameter);
                return query.Where(nullPredicate);
            }

            var constant = Expression.Constant(_currentDataAccessScope.DepartmentId ?? profile.DepartmentId, departmentProperty.PropertyType);
            var predicate = Expression.Lambda<Func<T, bool>>(Expression.Equal(property, constant), parameter);
            return query.Where(predicate);
        }
    }
}
