namespace OfficeAutomation.Modules.AiEngine;

/// <summary>
/// Resolves ambiguous line-item classifications (e.g. whether a number is a discount
/// or tax) using an external LLM provider when available, with a deterministic
/// rule-based fallback for low-confidence OCR or provider outages.
/// </summary>
public interface ISemanticDisambiguationService
{
    /// <summary>
    /// Classifies a single ambiguous line item. Falls back to heuristics when the
    /// LLM provider is unavailable or the OCR confidence is below the threshold.
    /// </summary>
    Task<LineItemResolution> DisambiguateLineAsync(
        AmbiguousLineItem line,
        CancellationToken cancellationToken);

    /// <summary>
    /// Classifies a batch of ambiguous line items, returning a resolution per input.
    /// </summary>
    Task<IReadOnlyList<LineItemResolution>> DisambiguateLinesAsync(
        IReadOnlyList<AmbiguousLineItem> lines,
        CancellationToken cancellationToken);
}