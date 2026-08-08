using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OfficeAutomation.Modules.Persistence;
using OfficeAutomation.Modules.Persistence.Entities;

namespace OfficeAutomation.Modules.AiEngine;

/// <inheritdoc />
public sealed class EntityResolutionService : IEntityResolutionService
{
    private readonly IDbContextFactory<TaxDbContext> _dbFactory;
    private readonly ILogger<EntityResolutionService> _logger;

    public EntityResolutionService(
        IDbContextFactory<TaxDbContext> dbFactory,
        ILogger<EntityResolutionService> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    public async Task<ResolvedEntity> ResolveSellerAsync(string? rawName, CancellationToken cancellationToken)
    {
        return await ResolveAsync(rawName, "Seller", cancellationToken);
    }

    public async Task<ResolvedEntity> ResolveBuyerAsync(string? rawName, CancellationToken cancellationToken)
    {
        return await ResolveAsync(rawName, "Buyer", cancellationToken);
    }

    private async Task<ResolvedEntity> ResolveAsync(
        string? rawName,
        string entityType,
        CancellationToken cancellationToken)
    {
        var original = (rawName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(original))
        {
            return new ResolvedEntity { EntityType = entityType, OriginalName = original, CanonicalName = original, Score = 0 };
        }

        var normalized = Normalize(original);

        using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var best = entityType == "Seller"
            ? await FindBestMatchAsync(
                db.Sellers.AsNoTracking().Select(e => new PartyCandidate(e.Id, e.Name)),
                normalized,
                cancellationToken)
            : await FindBestMatchAsync(
                db.Buyers.AsNoTracking().Select(e => new PartyCandidate(e.Id, e.Name)),
                normalized,
                cancellationToken);

        if (best is null)
        {
            return new ResolvedEntity { EntityType = entityType, OriginalName = original, CanonicalName = original, Score = 0 };
        }

        _logger.LogInformation(
            "Resolved {EntityType} '{Original}' -> '{Canonical}' (score {Score:F2}).",
            entityType, original, best.Name, best.Score);

        return new ResolvedEntity
        {
            EntityId = best.Id,
            EntityType = entityType,
            OriginalName = original,
            CanonicalName = best.Name,
            Score = best.Score
        };
    }

    private async Task<BestMatch?> FindBestMatchAsync(
        IQueryable<PartyCandidate> candidates,
        string normalized,
        CancellationToken cancellationToken)
    {
        var list = await candidates.ToListAsync(cancellationToken);
        if (list.Count == 0)
        {
            return null;
        }

        BestMatch? best = null;
        foreach (var candidate in list)
        {
            var score = Similarity(normalized, Normalize(candidate.Name));
            if (best is null || score > best.Score)
            {
                best = new BestMatch(candidate.Id, candidate.Name, score);
            }
        }

        return best;
    }

    /// <summary>
    /// Normalizes a Persian party name: trims, collapses whitespace, strips Zero-Width
    /// Non-Joiner (ZWNJ) and unifies Persian/Arabic letter variants.
    /// </summary>
    public static string Normalize(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        var sb = new System.Text.StringBuilder(input.Length);
        foreach (var ch in input)
        {
            if (char.IsWhiteSpace(ch))
            {
                if (sb.Length > 0 && sb[^1] != ' ')
                {
                    sb.Append(' ');
                }

                continue;
            }

            // ZWNJ (U+200C) is treated as a space separator to unify "دیجی کالا" / "دیجیکالا".
            if (ch == '\u200C')
            {
                if (sb.Length > 0 && sb[^1] != ' ')
                {
                    sb.Append(' ');
                }

                continue;
            }

            sb.Append(NormalizeLetter(ch));
        }

        return sb.ToString().Trim().ToLowerInvariant();
    }

    private static char NormalizeLetter(char ch) => ch switch
    {
        '\u064A' or '\u0649' => '\u06CC', // ya -> Persian ye
        '\u0643' => '\u06A9',              // kaf -> Persian ke
        '\u0629' => '\u0647',              // teh marbuta -> he
        _ => ch
    };

    // ──────────────────────────────────────────────────────────────────────────
    // Similarity (Dice coefficient over bigrams + containment bonus)
    // ──────────────────────────────────────────────────────────────────────────

    private static double Similarity(string a, string b)
    {
        if (string.Equals(a, b, StringComparison.Ordinal))
        {
            return 1.0;
        }

        if (a.Length == 0 && b.Length == 0)
        {
            return 1.0;
        }

        if (a.Length == 0 || b.Length == 0)
        {
            return 0.0;
        }

        var bigramsA = Bigrams(a);
        var bigramsB = Bigrams(b);

        var intersection = bigramsA.Count(b => bigramsB.Contains(b));
        var dice = (2.0 * intersection) / (bigramsA.Count + bigramsB.Count);

        // Bonus when one name is fully contained in the other (e.g. "دیجی کالا" in "فروشگاه دیجی کالا").
        if (a.Length >= 4 && (b.Contains(a, StringComparison.Ordinal) || a.Contains(b, StringComparison.Ordinal)))
        {
            dice = Math.Max(dice, 0.9);
        }

        return Math.Clamp(dice, 0.0, 1.0);
    }

    private static List<string> Bigrams(string input)
    {
        var result = new List<string>(input.Length - 1);
        for (int i = 0; i < input.Length - 1; i++)
        {
            result.Add(input.Substring(i, 2));
        }

        return result;
    }

    private sealed record BestMatch(Guid Id, string Name, double Score);

    private sealed record PartyCandidate(Guid Id, string Name);
}