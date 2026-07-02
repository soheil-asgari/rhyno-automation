using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using OfficeAutomation.Services.Security;

namespace OfficeAutomation.Data
{
    public sealed class RowLevelSecuritySaveChangesInterceptor : SaveChangesInterceptor
    {
        private readonly ICurrentDataAccessScope _scope;

        public RowLevelSecuritySaveChangesInterceptor(ICurrentDataAccessScope scope)
        {
            _scope = scope;
        }

        public override InterceptionResult<int> SavingChanges(
            DbContextEventData eventData,
            InterceptionResult<int> result)
        {
            EnforceScope(eventData.Context);
            return result;
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            EnforceScope(eventData.Context);
            return ValueTask.FromResult(result);
        }

        private void EnforceScope(DbContext? context)
        {
            if (context == null || !_scope.IsInitialized || !_scope.IsAuthenticated || _scope.HasGlobalAccess)
            {
                return;
            }

            foreach (var entry in context.ChangeTracker.Entries()
                         .Where(item => item.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
            {
                var property = entry.Properties.FirstOrDefault(item =>
                    string.Equals(item.Metadata.Name, "DepartmentId", StringComparison.Ordinal) &&
                    (item.Metadata.ClrType == typeof(int) || item.Metadata.ClrType == typeof(int?)));

                if (property == null)
                {
                    continue;
                }

                if (!_scope.DepartmentId.HasValue)
                {
                    throw new InvalidOperationException($"Row-level security denied write access to entity '{entry.Metadata.ClrType.Name}'.");
                }

                if (entry.State == EntityState.Added && property.CurrentValue == null)
                {
                    property.CurrentValue = _scope.DepartmentId;
                }

                var value = property.CurrentValue ?? property.OriginalValue;
                var departmentId = value as int? ?? (value is int intValue ? intValue : null);
                if (departmentId != _scope.DepartmentId)
                {
                    throw new InvalidOperationException(
                        $"Row-level security denied {entry.State} on entity '{entry.Metadata.ClrType.Name}' outside department scope.");
                }
            }
        }
    }
}
