namespace OfficeAutomation.Modules.AiEngine;

/// <summary>
/// A line item whose financial meaning is ambiguous and needs classification.
/// </summary>
public sealed class AmbiguousLineItem
{
    /// <summary>Raw description / OCR text for the line.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Numeric value observed on the invoice (in Rial).</summary>
    public decimal? Amount { get; set; }

    /// <summary>OCR confidence (0-100) for the extraction of this line.</summary>
    public double OcrConfidence { get; set; }

    /// <summary>Candidate interpretations the caller already suspects.</summary>
    public List<string> CandidateKinds { get; set; } = new();
}

/// <summary>
/// The resolved interpretation of an ambiguous line item.
/// </summary>
public sealed class LineItemResolution
{
    /// <summary>One of: Item, Discount, Tax, Duty, Shipping, Insurance.</summary>
    public string Kind { get; set; } = "Item";

    /// <summary>Confidence of the chosen interpretation (0.0 - 1.0).</summary>
    public double Confidence { get; set; }

    /// <summary>Human-readable reasoning in Persian.</summary>
    public string? Rationale { get; set; }

    /// <summary>True when the resolution came from the external LLM provider.</summary>
    public bool UsedLlm { get; set; }

    /// <summary>True when the result was produced by the local rule-based fallback.</summary>
    public bool UsedFallback => !UsedLlm;
}

/// <summary>
/// A semantic classification request covering core fields that may be ambiguous.
/// </summary>
public sealed class SemanticClassificationResult
{
    public string? BuyerName { get; set; }
    public string? SellerName { get; set; }
    public string? InvoiceNumber { get; set; }
    public string? DateShamsi { get; set; }
    public decimal? TotalAmount { get; set; }
    public List<LineItemResolution> LineResolutions { get; set; } = new();
}