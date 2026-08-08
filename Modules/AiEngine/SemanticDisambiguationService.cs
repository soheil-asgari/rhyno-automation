using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace OfficeAutomation.Modules.AiEngine;

/// <inheritdoc />
public sealed class SemanticDisambiguationService : ISemanticDisambiguationService
{
    /// <summary>Below this OCR confidence the LLM is skipped and heuristics are used.</summary>
    public const double FallbackConfidenceThreshold = 40.0;

    private static readonly HashSet<string> KnownKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        "Item", "Discount", "Tax", "Duty", "Shipping", "Insurance"
    };

    private readonly AiService _ai;
    private readonly ILogger<SemanticDisambiguationService> _logger;

    public SemanticDisambiguationService(AiService ai, ILogger<SemanticDisambiguationService> logger)
    {
        _ai = ai;
        _logger = logger;
    }

    public async Task<IReadOnlyList<LineItemResolution>> DisambiguateLinesAsync(
        IReadOnlyList<AmbiguousLineItem> lines,
        CancellationToken cancellationToken)
    {
        if (lines is null || lines.Count == 0)
        {
            return [];
        }

        var results = new List<LineItemResolution>(lines.Count);
        foreach (var line in lines)
        {
            results.Add(await DisambiguateLineAsync(line, cancellationToken));
        }

        return results;
    }

    public async Task<LineItemResolution> DisambiguateLineAsync(
        AmbiguousLineItem line,
        CancellationToken cancellationToken)
    {
        if (line.OcrConfidence < FallbackConfidenceThreshold)
        {
            _logger.LogDebug(
                "OCR confidence {Confidence} below threshold; using rule-based fallback for line '{Description}'.",
                line.OcrConfidence, line.Description);
            return ResolveByHeuristics(line);
        }

        try
        {
            var prompt = BuildPrompt(line);
            var reply = await _ai.AskAsync(prompt, cancellationToken);
            var parsed = TryParseLlmReply(reply);
            if (parsed is not null)
            {
                return parsed;
            }

            _logger.LogWarning("LLM returned an unparseable reply for line '{Description}'; falling back.", line.Description);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "LLM disambiguation failed; using rule-based fallback for line '{Description}'.", line.Description);
        }

        return ResolveByHeuristics(line);
    }
// ──────────────────────────────────────────────────────────────────────────
    // LLM orchestration
    // ──────────────────────────────────────────────────────────────────────────

    private static string BuildPrompt(AmbiguousLineItem line)
    {
        var candidates = line.CandidateKinds.Count > 0
            ? string.Join(", ", line.CandidateKinds)
            : "Item, Discount, Tax, Duty, Shipping, Insurance";

        return
            "You are a meticulous Iranian tax accountant. Classify ONE ambiguous invoice line item. " +
            "Decide whether the observed number represents a discount, tax/duty, shipping cost, or a plain item.\n\n" +
            $"Description: {line.Description}\n" +
            $"Amount (Rial): {line.Amount?.ToString("N0") ?? "unknown"}\n" +
            $"Candidate kinds: {candidates}\n\n" +
            "Reply with ONLY a JSON object in this exact shape (no markdown): " +
            "{\"kind\":\"Item|Discount|Tax|Duty|Shipping|Insurance\",\"confidence\":0.0,\"rationale\":\"short Persian reason\"}";
    }

    private static LineItemResolution? TryParseLlmReply(string reply)
    {
        var trimmed = reply.Trim();
        if (trimmed.Length == 0)
        {
            return null;
        }

        // Strip leading/trailing markdown fences if present.
        trimmed = trimmed.Trim('`');
        if (trimmed.StartsWith("json", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[4..].TrimStart();
        }

        try
        {
            using var doc = JsonDocument.Parse(trimmed);
            var root = doc.RootElement;

            var kind = root.TryGetProperty("kind", out var k) ? k.GetString() : null;
            if (string.IsNullOrWhiteSpace(kind) || !KnownKinds.Contains(kind))
            {
                return null;
            }

            var confidence = root.TryGetProperty("confidence", out var c) && c.TryGetDouble(out var cd)
                ? Math.Clamp(cd, 0.0, 1.0)
                : 0.5;

            var rationale = root.TryGetProperty("rationale", out var r) ? r.GetString() : null;

            return new LineItemResolution
            {
                Kind = NormalizeKind(kind),
                Confidence = confidence,
                Rationale = rationale,
                UsedLlm = true
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Rule-based fallback
    // ──────────────────────────────────────────────────────────────────────────

    private static LineItemResolution ResolveByHeuristics(AmbiguousLineItem line)
    {
        var desc = (line.Description ?? string.Empty).ToLowerInvariant();

        if (ContainsAny(desc, "مالیات", "ارزش افزوده", "vat", "tax", "%۱۰", "٪10"))
        {
            return new LineItemResolution { Kind = "Tax", Confidence = 0.85, Rationale = "کلیدواژه مالیات/ارزش افزوده شناسایی شد." };
        }

        if (ContainsAny(desc, "حق بیمه", "بیمه", "insurance"))
        {
            return new LineItemResolution { Kind = "Insurance", Confidence = 0.85, Rationale = "کلیدواژه بیمه شناسایی شد." };
        }

        if (ContainsAny(desc, "حمل", "کرایه", "shipping", "carriage"))
        {
            return new LineItemResolution { Kind = "Shipping", Confidence = 0.85, Rationale = "کلیدواژه حمل/کرایه شناسایی شد." };
        }

        if (ContainsAny(desc, "تخفیف", "discount", "تخفیفات"))
        {
            return new LineItemResolution { Kind = "Discount", Confidence = 0.9, Rationale = "کلیدواژه تخفیف شناسایی شد." };
        }

        if (ContainsAny(desc, "عوارض", "duty"))
        {
            return new LineItemResolution { Kind = "Duty", Confidence = 0.6, Rationale = "کلیدواژه عوارض شناسایی شد." };
        }

        // Default: treat as a regular line item.
        return new LineItemResolution
        {
            Kind = "Item",
            Confidence = 0.5,
            Rationale = "هیچ شواهد زبانی برای تخفیف/مالیات یافت نشد؛ به‌عنوان قلم کالا تلقی شد."
        };
    }

    private static bool ContainsAny(string text, params string[] needles)
    {
        foreach (var needle in needles)
        {
            if (text.Contains(needle, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string NormalizeKind(string kind)
    {
        // "Tax" and "Duty" are distinct line kinds; keep the LLM's canonical casing.
        return KnownKinds.FirstOrDefault(k => string.Equals(k, kind, StringComparison.OrdinalIgnoreCase)) ?? "Item";
    }
}