using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using OfficeAutomation.Models;
using OfficeAutomation.Services.Auditing;

namespace OfficeAutomation.Data
{
    public sealed class AuditSaveChangesInterceptor : SaveChangesInterceptor
    {
        private readonly IAuditContextProvider _auditContextProvider;
        private readonly IAuditLogger _auditLogger;
        private bool _isWritingAuditLog;

        public AuditSaveChangesInterceptor(
            IAuditContextProvider auditContextProvider,
            IAuditLogger auditLogger)
        {
            _auditContextProvider = auditContextProvider;
            _auditLogger = auditLogger;
        }

        public override async ValueTask<int> SavedChangesAsync(
            SaveChangesCompletedEventData eventData,
            int result,
            CancellationToken cancellationToken = default)
        {
            var context = eventData.Context;
            if (context is not IAuditableDbContext dbContext || _isWritingAuditLog)
            {
                return result;
            }

            var auditEntries = dbContext.DequeuePendingAuditEntries();
            if (auditEntries.Count == 0)
            {
                return result;
            }

            foreach (var auditEntry in auditEntries)
            {
                dbContext.AuditLogs.Add(_auditLogger.Create(auditEntry));
            }

            _isWritingAuditLog = true;
            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            finally
            {
                _isWritingAuditLog = false;
            }

            return result;
        }

        public override InterceptionResult<int> SavingChanges(
            DbContextEventData eventData,
            InterceptionResult<int> result)
        {
            CaptureAuditEntries(eventData.Context);
            return result;
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            CaptureAuditEntries(eventData.Context);
            return ValueTask.FromResult(result);
        }

        private void CaptureAuditEntries(DbContext? context)
        {
            if (context is not IAuditableDbContext dbContext || _isWritingAuditLog)
            {
                return;
            }

            dbContext.ChangeTracker.DetectChanges();
            dbContext.SetPendingAuditEntries(dbContext.PrepareAuditEntries(_auditContextProvider.GetCurrent()));
        }
    }
}
