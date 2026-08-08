namespace OfficeAutomation.Modules.InvoiceIntake;

/// <summary>
/// Stores transient intake items so they survive across HTTP requests for the same user,
/// while keeping everything in-process for Phase 1 (no database dependency).
/// </summary>
public interface IIntakeSessionStore
{
    /// <summary>All items belonging to a user/owner key.</summary>
    IReadOnlyList<InvoiceIntakeItem> GetAll(string ownerKey);

    /// <summary>Returns an item or null when missing / not owned by the owner key.</summary>
    InvoiceIntakeItem? Get(string ownerKey, Guid id);

    /// <summary>Adds a new item. Returns false when the owner already has the maximum count.</summary>
    bool Add(string ownerKey, InvoiceIntakeItem item, int maxItemsPerOwner);

    bool Remove(string ownerKey, Guid id);

    int Count(string ownerKey);
}
