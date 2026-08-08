namespace OfficeAutomation.Modules.Ocr;

/// <summary>
/// Configuration for the OCR engine, bound from the "Ocr" configuration section.
/// </summary>
public sealed class OcrOptions
{
    public const string SectionName = "Ocr";

    /// <summary>
    /// Directory that contains the Tesseract *.traineddata language files.
    /// When null or empty, the engine falls back to App_Data/tessdata under the content root.
    /// </summary>
    public string? TessDataPath { get; set; }

    /// <summary>
    /// The languages to use for recognition (Tesseract '+' separated, e.g. "fas+eng").
    /// </summary>
    public string DefaultLanguages { get; set; } = "fas+eng";

    /// <summary>
    /// Maximum accepted intake file size in bytes.
    /// </summary>
    public long MaxFileBytes { get; set; } = 20 * 1024 * 1024;
}
