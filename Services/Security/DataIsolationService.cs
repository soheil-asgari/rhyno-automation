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

        public DataIsolationService(ICurrentUserContextAccessor currentUserContextAccessor)
        {
            _currentUserContextAccessor = currentUserContextAccessor;
        }

        public async Task<IQueryable<T>> ApplyDepartmentScopeAsync<T>(IQueryable<T> query, CancellationToken cancellationToken = default)
            where T : class
        {
            var departmentProperty = typeof(T).GetProperty("DepartmentId");
            if (departmentProperty == null || (departmentProperty.PropertyType != typeof(int) && departmentProperty.PropertyType != typeof(int?)))
            {
                return query;
            }

            var profile = await _currentUserContextAccessor.GetAccessProfileAsync(cancellationToken);
            if (profile == null)
            {
                return query.Where(_ => false);
            }

            if (profile.HasGlobalAccess)
            {
                return query;
            }

            if (!profile.DepartmentId.HasValue)
            {
                return query.Where(_ => false);
            }

            var parameter = Expression.Parameter(typeof(T), "entity");
            var property = Expression.Property(parameter, departmentProperty);
            var constant = Expression.Constant(profile.DepartmentId, departmentProperty.PropertyType);
            var predicate = Expression.Lambda<Func<T, bool>>(Expression.Equal(property, constant), parameter);
            return query.Where(predicate);
        }
    }
}
