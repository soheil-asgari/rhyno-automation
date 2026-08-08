namespace OfficeAutomation.Modules.Ocr;

/// <summary>
/// The outcome of a single OCR recognition attempt.
/// A failed attempt is still represented as a result so callers can handle it gracefully
/// instead of breaking on an exception.
/// </summary>
public sealed record OcrResult(
    bool IsSuccess,
    string RawText,
    string Language,
    double MeanConfidence,
    int PageCount,
    long DurationMilliseconds,
    string? ErrorMessage = null)
{
    public static OcrResult Failure(string errorMessage, string? language = null) =>
        new(false, string.Empty, language ?? string.Empty, 0, 0, 0, errorMessage);

    public static OcrResult Empty(string language = "") =>
        new(true, string.Empty, language, 0, 0, 0);

    public bool HasMeaningfulText => IsSuccess && !string.IsNullOrWhiteSpace(RawText);
}
