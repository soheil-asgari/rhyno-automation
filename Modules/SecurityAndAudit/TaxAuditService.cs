using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OfficeAutomation.Models;
using OfficeAutomation.Modules.Persistence;

namespace OfficeAutomation.Modules.SecurityAndAudit;

/// <summary>A single audit trail entry for the tax module.</summary>
public sealed class TaxAuditEntry
{
    public Guid Id { get; set; }
    public string? UserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public DateTimeOffset DateTime { get; set; }
    public string? Details { get; set; }
    public string? UserIP { get; set; }
}

/// <summary>
/// Writes explicit, human-readable audit entries for tax submission, export and
/// security actions on top of the automatic EF Core change-tracking interceptor.
/// </summary>
public interface ITaxAuditService
{
    /// <summary>Records an explicit audit action (e.g. Submission, Export, LoginAttempt).</summary>
    Task RecordAsync(
        string action,
        string entityName,
        string? entityId,
        string? details,
        string? userId,
        string? userIp,
        CancellationToken cancellationToken);

    /// <summary>Returns the most recent audit entries for the tax module.</summary>
    Task<IReadOnlyList<TaxAuditEntry>> GetRecentAsync(int take, CancellationToken cancellationToken);

    /// <summary>Returns a filtered set of audit entries by action and entity.</summary>
    Task<IReadOnlyList<TaxAuditEntry>> QueryAsync(
        string? action,
        string? search,
        int take,
        CancellationToken cancellationToken);
}

/// <inheritdoc />
public sealed class TaxAuditService : ITaxAuditService
{
    private readonly IDbContextFactory<TaxDbContext> _dbFactory;
    private readonly ILogger<TaxAuditService> _logger;

    public TaxAuditService(
        IDbContextFactory<TaxDbContext> dbFactory,
        ILogger<TaxAuditService> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    public async Task RecordAsync(
        string action,
        string entityName,
        string? entityId,
        string? details,
        string? userId,
        string? userIp,
        CancellationToken cancellationToken)
    {
        using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var entry = new AuditLog
        {
            UserId = !string.IsNullOrWhiteSpace(userId) ? userId : "system",
            Action = action,
            TableName = entityName,
            EntityId = entityId,
            DateTime = DateTimeOffset.UtcNow,
            UserIP = userIp,
            NewValues = details,
            Severity = "Information",
            ComplianceCategory = "Tax"
        };

        db.AuditLogs.Add(entry);
        await db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Tax audit recorded: {Action} on {Entity} ({EntityId}) by {User}.",
            action, entityName, entityId, entry.UserId);
    }

    public async Task<IReadOnlyList<TaxAuditEntry>> GetRecentAsync(int take, CancellationToken cancellationToken)
    {
        using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.AuditLogs
            .AsNoTracking()
            .Where(a => a.ComplianceCategory == "Tax")
            .OrderByDescending(a => a.DateTime)
            .Take(Math.Clamp(take, 1, 500))
            .Select(a => new TaxAuditEntry
            {
                Id = a.Id,
                UserId = a.UserId,
                Action = a.Action,
                EntityName = a.TableName,
                EntityId = a.EntityId,
                DateTime = a.DateTime,
                Details = a.NewValues,
                UserIP = a.UserIP
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TaxAuditEntry>> QueryAsync(
        string? action,
        string? search,
        int take,
        CancellationToken cancellationToken)
    {
        using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var query = db.AuditLogs
            .AsNoTracking()
            .Where(a => a.ComplianceCategory == "Tax")
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(action))
        {
            query = query.Where(a => a.Action == action);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(a =>
                a.TableName.Contains(s) ||
                (a.EntityId != null && a.EntityId.Contains(s)) ||
                (a.NewValues != null && a.NewValues.Contains(s)));
        }

        return await query
            .OrderByDescending(a => a.DateTime)
            .Take(Math.Clamp(take, 1, 500))
            .Select(a => new TaxAuditEntry
            {
                Id = a.Id,
                UserId = a.UserId,
                Action = a.Action,
                EntityName = a.TableName,
                EntityId = a.EntityId,
                DateTime = a.DateTime,
                Details = a.NewValues,
                UserIP = a.UserIP
            })
            .ToListAsync(cancellationToken);
    }
}