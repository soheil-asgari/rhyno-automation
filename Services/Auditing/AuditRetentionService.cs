using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OfficeAutomation.Modules.Platform.Infrastructure.Persistence;
using OfficeAutomation.Models;

namespace OfficeAutomation.Services.Auditing;

public sealed class AuditRetentionService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly PlatformDbContext _context;
    private readonly AuditRetentionOptions _options;
    private readonly ILogger<AuditRetentionService> _logger;

    public AuditRetentionService(
        PlatformDbContext context,
        IOptions<AuditRetentionOptions> options,
        ILogger<AuditRetentionService> logger)
    {
        _context = context;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<int> ArchiveExpiredAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return 0;
        }

        var cutoff = DateTimeOffset.UtcNow.AddDays(-Math.Max(1, _options.RetainDays));
        var batchSize = Math.Clamp(_options.BatchSize, 50, 5000);
        var archivedCount = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            var batch = await _context.AuditLogs
                .AsNoTracking()
                .Where(item => item.DateTime < cutoff)
                .OrderBy(item => item.DateTime)
                .Take(batchSize)
                .ToListAsync(cancellationToken);

            if (batch.Count == 0)
            {
                break;
            }

            await AppendArchiveAsync(batch, cancellationToken);
            _context.AuditLogs.RemoveRange(batch);
            await _context.SaveChangesAsync(cancellationToken);
            archivedCount += batch.Count;
        }

        if (archivedCount > 0)
        {
            _logger.LogInformation("Archived {AuditLogCount} audit log rows older than {Cutoff}.", archivedCount, cutoff);
        }

        return archivedCount;
    }

    private async Task AppendArchiveAsync(IReadOnlyCollection<AuditLog> batch, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_options.ArchivePath);
        var fileName = "audit-" + DateTimeOffset.UtcNow.ToString("yyyyMMdd") + ".jsonl";
        var path = Path.Combine(_options.ArchivePath, fileName);

        await using var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read);
        await using var writer = new StreamWriter(stream);
        foreach (var log in batch)
        {
            await writer.WriteLineAsync(JsonSerializer.Serialize(log, JsonOptions).AsMemory(), cancellationToken);
        }
    }
}


