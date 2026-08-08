namespace OfficeAutomation.Modules.Ocr;

/// <summary>
/// Abstraction over an OCR engine. Implementations must never throw for expected failures
/// (missing language data, unreadable image, etc.) and instead return a <see cref="OcrResult"/>.
/// </summary>
public interface IOcrEngine
{
    /// <summary>
    /// Recognizes text from raw image bytes (PNG/JPG/BMP/TIFF).
    /// </summary>
    /// <param name="imageBytes">Encoded raster image bytes.</param>
    /// <param name="language">Tesseract language string (e.g. "fas+eng"). Null uses the configured default.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<OcrResult> RecognizeAsync(byte[] imageBytes, string? language = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the list of traineddata language codes currently available on disk.
    /// </summary>
    Task<IReadOnlyList<string>> GetAvailableLanguagesAsync(CancellationToken cancellationToken = default);
}
