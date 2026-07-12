using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using OfficeAutomation.Models;
using OfficeAutomation.Services.Auditing;

namespace OfficeAutomation.Data
{
    /// <summary>
    /// Intercepts EF Core save operations to inject audit log entries into the same
    /// transaction, eliminating the previous "Double SaveChanges" anti-pattern that
    /// doubled the number of database round-trips per operation.
    ///
    /// Strategy:
    ///  - Entities WITHOUT temporary (DB-generated) properties: their AuditLog records
    ///    are created and attached to the ChangeTracker BEFORE the main save, so they
    ///    are persisted in the exact same SQL batch / transaction.
    ///  - Entities WITH temporary properties (e.g. IDENTITY PKs whose value is only
    ///    known after INSERT): their pending entries are stored and finalised in
    ///    SavedChangesAsync, but we write them via a *separate* lightweight call only
    ///    when strictly necessary (these are the minority of cases).
    /// </summary>
    public sealed class AuditSaveChangesInterceptor : SaveChangesInterceptor
    {
        private readonly IAuditContextProvider _auditContextProvider;
        private readonly IAuditLogger _auditLogger;

        // Reentrance guard: prevents the secondary SaveChanges (for deferred entries)
        // from triggering another audit cycle.
        private bool _isWritingAuditLog;

        public AuditSaveChangesInterceptor(
            IAuditContextProvider auditContextProvider,
            IAuditLogger auditLogger)
        {
            _auditContextProvider = auditContextProvider;
            _auditLogger = auditLogger;
        }

        // ──────────────────────────────────────────────────────────────────────────
        // PRE-SAVE: Capture changes and attach immediate audit entries to the context
        // so they are included in the SAME database transaction/batch.
        // ──────────────────────────────────────────────────────────────────────────

        public override InterceptionResult<int> SavingChanges(
            DbContextEventData eventData,
            InterceptionResult<int> result)
        {
            AttachImmediateAuditEntries(eventData.Context);
            return result;
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            AttachImmediateAuditEntries(eventData.Context);
            return ValueTask.FromResult(result);
        }

        // ──────────────────────────────────────────────────────────────────────────
        // POST-SAVE: Only handle the minority of "deferred" entries whose PK values
        // were not known until after the INSERT (e.g. DB-generated IDENTITY columns).
        // These still require a second SaveChanges, but the volume is minimal and
        // unavoidable — the important fix is that the majority no longer need it.
        // ──────────────────────────────────────────────────────────────────────────

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

            // Dequeue any entries that were deferred because they had temporary properties.
            var deferredEntries = dbContext.DequeuePendingAuditEntries();
            if (deferredEntries.Count == 0)
            {
                return result;
            }

            // Finalise the deferred entries now that DB-generated values are available.
            foreach (var auditEntry in deferredEntries)
            {
                auditEntry.FinalizeTemporaryProperties();
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

        // Synchronous post-save path (rarely used in modern ASP.NET, but kept for completeness).
        public override int SavedChanges(
            SaveChangesCompletedEventData eventData,
            int result)
        {
            var context = eventData.Context;
            if (context is not IAuditableDbContext dbContext || _isWritingAuditLog)
            {
                return result;
            }

            var deferredEntries = dbContext.DequeuePendingAuditEntries();
            if (deferredEntries.Count == 0)
            {
                return result;
            }

            foreach (var auditEntry in deferredEntries)
            {
                auditEntry.FinalizeTemporaryProperties();
                dbContext.AuditLogs.Add(_auditLogger.Create(auditEntry));
            }

            _isWritingAuditLog = true;
            try
            {
                // IAuditableDbContext only exposes SaveChangesAsync; use the base DbContext
                // synchronous path via the concrete context object.
                if (context is DbContext baseContext)
                {
                    baseContext.SaveChanges();
                }
            }
            finally
            {
                _isWritingAuditLog = false;
            }

            return result;
        }

        // ──────────────────────────────────────────────────────────────────────────
        // PRIVATE HELPERS
        // ──────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Prepares audit entries and splits them into two groups:
        ///   - Immediate: no DB-generated values pending → attached to the tracker
        ///     so they are saved in the same transaction as the triggering change.
        ///   - Deferred: have temporary (DB-generated) properties → stored for
        ///     finalisation in the post-save hook.
        /// </summary>
        private void AttachImmediateAuditEntries(DbContext? context)
        {
            if (context is not IAuditableDbContext dbContext || _isWritingAuditLog)
            {
                return;
            }

            dbContext.ChangeTracker.DetectChanges();
            var allEntries = dbContext.PrepareAuditEntries(_auditContextProvider.GetCurrent());

            var deferredEntries = new List<PendingAuditLogEntry>();

            foreach (var entry in allEntries)
            {
                if (entry.HasTemporaryProperties)
                {
                    // Cannot resolve entity ID yet — defer until after save.
                    deferredEntries.Add(entry);
                }
                else
                {
                    // Attach the AuditLog entity directly to the current context so it
                    // is included in the same SQL batch/transaction — zero extra round-trips.
                    dbContext.AuditLogs.Add(_auditLogger.Create(entry));
                }
            }

            // Store only the deferred entries for post-save finalisation.
            dbContext.SetPendingAuditEntries(deferredEntries);
        }
    }
}
