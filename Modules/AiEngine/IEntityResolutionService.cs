namespace OfficeAutomation.Modules.AiEngine;

/// <summary>
/// A resolved canonical entity matched from a raw party name.
/// </summary>
public sealed class ResolvedEntity
{
    /// <summary>Matched entity id (null when no persisted match was found).</summary>
    public Guid? EntityId { get; set; }

    /// <summary>Canonical display name.</summary>
    public string CanonicalName { get; set; } = string.Empty;

    /// <summary>Raw name that was resolved.</summary>
    public string OriginalName { get; set; } = string.Empty;

    /// <summary>Similarity score 0.0 - 1.0 towards the matched canonical name.</summary>
    public double Score { get; set; }

    /// <summary>"Seller", "Buyer", or "Unknown".</summary>
    public string EntityType { get; set; } = "Unknown";

    /// <summary>True when a persisted entity was matched.</summary>
    public bool IsMatched => EntityId.HasValue && Score >= 0.55;
}

/// <summary>
/// Fuzzy-matches and canonicalizes seller/buyer name variations (e.g. "دیجی کالا",
/// "دیجیکالا", "نوآوران فن‌آوری پلاستیک") to a unified stored entity.
/// </summary>
public interface IEntityResolutionService
{
    Task<ResolvedEntity> ResolveSellerAsync(string? rawName, CancellationToken cancellationToken);
    Task<ResolvedEntity> ResolveBuyerAsync(string? rawName, CancellationToken cancellationToken);
}