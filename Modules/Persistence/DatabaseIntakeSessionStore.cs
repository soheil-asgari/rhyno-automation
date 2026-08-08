using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OfficeAutomation.Modules.InvoiceIntake;
using OfficeAutomation.Modules.Persistence.Entities;

namespace OfficeAutomation.Modules.Persistence;

/// <summary>
/// EF Core-backed implementation of <see cref="IIntakeSessionStore"/>.
/// Replaces the in-memory store so intake items survive process restarts
/// and are queryable via SQL.
/// </summary>
public sealed class DatabaseIntakeSessionStore : IIntakeSessionStore
{
    private readonly IDbContextFactory<TaxDbContext> _dbFactory;
    private readonly ILogger<DatabaseIntakeSessionStore> _logger;

    public DatabaseIntakeSessionStore(
        IDbContextFactory<TaxDbContext> dbFactory,
        ILogger<DatabaseIntakeSessionStore> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    public IReadOnlyList<InvoiceIntakeItem> GetAll(string ownerKey)
    {
        try
        {
            using var db = _dbFactory.CreateDbContext();
            var records = db.InvoiceRecords
                .Where(r => r.OwnerKey == ownerKey)
                .OrderBy(r => r.CreatedAt)
                .ToList();

            return records.Select(MapToIntakeItem).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get intake items for owner {OwnerKey}", ownerKey);
            return Array.Empty<InvoiceIntakeItem>();
        }
    }

    public InvoiceIntakeItem? Get(string ownerKey, Guid id)
    {
        try
        {
            using var db = _dbFactory.CreateDbContext();
            var record = db.InvoiceRecords
                .Include(r => r.LineItems)
                .Include(r => r.Seller)
                .Include(r => r.Buyer)
                .FirstOrDefault(r => r.Id == id && r.OwnerKey == ownerKey);

            return record is null ? null : MapToIntakeItem(record);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get intake item {Id} for owner {OwnerKey}", id, ownerKey);
            return null;
        }
    }

    public bool Add(string ownerKey, InvoiceIntakeItem item, int maxItemsPerOwner)
    {
        try
        {
            using var db = _dbFactory.CreateDbContext();

            var currentCount = db.InvoiceRecords.Count(r => r.OwnerKey == ownerKey);
            if (currentCount >= maxItemsPerOwner)
            {
                return false;
            }

            var record = MapToRecord(item, ownerKey);
            db.InvoiceRecords.Add(record);
            db.SaveChanges();

            _logger.LogInformation("Added intake item {Id} for owner {OwnerKey}", record.Id, ownerKey);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add intake item {Id} for owner {OwnerKey}", item.Id, ownerKey);
            return false;
        }
    }

    public bool Remove(string ownerKey, Guid id)
    {
        try
        {
            using var db = _dbFactory.CreateDbContext();
            var record = db.InvoiceRecords
                .FirstOrDefault(r => r.Id == id && r.OwnerKey == ownerKey);

            if (record is null)
            {
                return false;
            }

            db.InvoiceRecords.Remove(record);
            db.SaveChanges();

            _logger.LogInformation("Removed intake item {Id} for owner {OwnerKey}", id, ownerKey);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove intake item {Id} for owner {OwnerKey}", id, ownerKey);
            return false;
        }
    }

    public int Count(string ownerKey)
    {
        try
        {
            using var db = _dbFactory.CreateDbContext();
            return db.InvoiceRecords.Count(r => r.OwnerKey == ownerKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to count intake items for owner {OwnerKey}", ownerKey);
            return 0;
        }
    }

    private static InvoiceRecord MapToRecord(InvoiceIntakeItem item, string ownerKey)
    {
        return new InvoiceRecord
        {
            Id = item.Id,
            InvoiceNumber = item.Extracted?.InvoiceNumber ?? string.Empty,
            DateShamsi = item.Extracted?.DateShamsi ?? string.Empty,
            InvoiceDate = item.Extracted?.Date,
            TotalAmount = item.Extracted?.TotalAmount ?? 0,
            RawOcrText = item.RawOcrText,
            Status = item.Status.ToString(),
            OwnerKey = ownerKey,
            CreatedAt = item.UploadedAt.DateTime
        };
    }

    private static InvoiceIntakeItem MapToIntakeItem(InvoiceRecord record)
    {
        return new InvoiceIntakeItem
        {
            Id = record.Id,
            OriginalFileName = record.InvoiceNumber,
            Status = Enum.TryParse<IntakeFileStatus>(record.Status, out var status)
                ? status
                : IntakeFileStatus.Completed,
            RawOcrText = record.RawOcrText,
            UploadedAt = record.CreatedAt,
            Extracted = new OfficeAutomation.Modules.Ocr.ExtractedInvoice
            {
                InvoiceNumber = record.InvoiceNumber,
                DateShamsi = record.DateShamsi,
                Date = record.InvoiceDate,
                TotalAmount = record.FinalAmount > 0 ? record.FinalAmount : record.TotalAmount,
                SellerName = record.Seller?.Name ?? string.Empty,
                BuyerName = record.Buyer?.Name ?? string.Empty
            }
        };
    }
}
