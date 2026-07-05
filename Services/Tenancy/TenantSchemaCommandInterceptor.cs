using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace OfficeAutomation.Services.Tenancy
{
    public sealed class TenantSchemaCommandInterceptor : DbCommandInterceptor
    {
        private readonly ICurrentTenantAccessor _tenantAccessor;

        public TenantSchemaCommandInterceptor(ICurrentTenantAccessor tenantAccessor)
        {
            _tenantAccessor = tenantAccessor;
        }

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result)
        {
            ReplaceSchema(command);
            return result;
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            ReplaceSchema(command);
            return ValueTask.FromResult(result);
        }

        public override InterceptionResult<int> NonQueryExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result)
        {
            ReplaceSchema(command);
            return result;
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            ReplaceSchema(command);
            return ValueTask.FromResult(result);
        }

        public override InterceptionResult<object> ScalarExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<object> result)
        {
            ReplaceSchema(command);
            return result;
        }

        public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<object> result,
            CancellationToken cancellationToken = default)
        {
            ReplaceSchema(command);
            return ValueTask.FromResult(result);
        }

        private void ReplaceSchema(DbCommand command)
        {
            var tenant = _tenantAccessor.Tenant;
            if (tenant == null || string.IsNullOrWhiteSpace(tenant.DatabaseSchema) || string.Equals(tenant.DatabaseSchema, "dbo", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var schema = tenant.DatabaseSchema;
            var text = command.CommandText;

            // Dynamically replace default SQL Server [dbo]. schema qualifier with the tenant's schema
            text = text.Replace("[dbo].", $"[{schema}].", StringComparison.OrdinalIgnoreCase);
            text = text.Replace(" dbo.", $" {schema}.", StringComparison.OrdinalIgnoreCase);

            command.CommandText = text;
        }
    }
}
