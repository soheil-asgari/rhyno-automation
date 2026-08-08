using System.Collections.Concurrent;

namespace OfficeAutomation.Modules.InvoiceIntake;

/// <summary>
/// Thread-safe in-memory intake store. Items are keyed per owner (user id) and survive
/// across requests, but are lost on process restart — acceptable for Phase 1 intake.
/// </summary>
public sealed class InMemoryIntakeSessionStore : IIntakeSessionStore
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<Guid, InvoiceIntakeItem>> _byOwner = new();

    public IReadOnlyList<InvoiceIntakeItem> GetAll(string ownerKey)
    {
        if (_byOwner.TryGetValue(ownerKey, out var owner))
        {
            return owner.Values.OrderBy(item => item.UploadedAt).ToList();
        }

        return Array.Empty<InvoiceIntakeItem>();
    }

    public InvoiceIntakeItem? Get(string ownerKey, Guid id)
        => _byOwner.TryGetValue(ownerKey, out var owner) && owner.TryGetValue(id, out var item)
            ? item
            : null;

    public bool Add(string ownerKey, InvoiceIntakeItem item, int maxItemsPerOwner)
    {
        var owner = _byOwner.GetOrAdd(ownerKey, _ => new ConcurrentDictionary<Guid, InvoiceIntakeItem>());
        if (owner.Count >= maxItemsPerOwner)
        {
            return false;
        }

        return owner.TryAdd(item.Id, item);
    }

    public bool Remove(string ownerKey, Guid id)
        => _byOwner.TryGetValue(ownerKey, out var owner) && owner.TryRemove(id, out _);

    public int Count(string ownerKey)
        => _byOwner.TryGetValue(ownerKey, out var owner) ? owner.Count : 0;
}
