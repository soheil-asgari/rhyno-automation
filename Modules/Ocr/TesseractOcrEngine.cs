using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Tesseract;

namespace OfficeAutomation.Modules.Ocr;

/// <summary>
/// Tesseract 5.x OCR engine integration with Persian (+English) language support.
///
/// The engine is resilient by design: when language traineddata is missing, an image is
/// unreadable, or OCR fails for any reason, it returns a <see cref="OcrResult.Failure"/>
/// instead of throwing, so callers can respond gracefully.
/// </summary>
public sealed class TesseractOcrEngine : IOcrEngine
{
    private readonly OcrOptions _options;
    private readonly string _contentRoot;

    public TesseractOcrEngine(IOptions<OcrOptions> options, IHostEnvironment environment)
    {
        _options = options.Value;
        _contentRoot = environment.ContentRootPath;
    }

    /// <inheritdoc />
    public Task<OcrResult> RecognizeAsync(
        byte[] imageBytes,
        string? language = null,
        CancellationToken cancellationToken = default)
    {
        if (imageBytes is null || imageBytes.Length == 0)
        {
            return Task.FromResult(OcrResult.Failure("داده تصویر خالی است.", language));
        }

        var tessDataPath = ResolveTessDataPath();
        if (string.IsNullOrWhiteSpace(tessDataPath) || !Directory.Exists(tessDataPath))
        {
            return Task.FromResult(OcrResult.Failure("داده زبانی موتور OCR (tessdata) یافت نشد. لطفاً فایل‌های زبان را نصب کنید.", language));
        }

        var languageCode = NormalizeLanguage(language);
        if (string.IsNullOrWhiteSpace(languageCode))
        {
            return Task.FromResult(OcrResult.Failure("زبان OCR پیکربندی نشده است.", language));
        }

        return Task.Run(() =>
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                using var engine = CreateEngine(tessDataPath, languageCode);
                using var pix = Pix.LoadFromMemory(imageBytes);
                using var page = engine.Process(pix);

                var text = page.GetText() ?? string.Empty;
                var confidence = page.GetMeanConfidence();

                return new OcrResult(
                    IsSuccess: true,
                    RawText: text.Trim(),
                    Language: languageCode,
                    MeanConfidence: confidence,
                    PageCount: 1,
                    DurationMilliseconds: stopwatch.ElapsedMilliseconds);
            }
            catch (TesseractException ex)
            {
                return OcrResult.Failure(TesseractErrorMessage(ex), languageCode);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IOException)
            {
                return OcrResult.Failure($"خطا در پردازش تصویر: {ex.Message}", languageCode);
            }
            finally
            {
                stopwatch.Stop();
            }
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> GetAvailableLanguagesAsync(CancellationToken cancellationToken = default)
    {
        var path = ResolveTessDataPath();
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        }

        var languages = Directory
            .EnumerateFiles(path, "*.traineddata", SearchOption.TopDirectoryOnly)
            .Select(file => Path.GetFileNameWithoutExtension(file))
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Where(code => !string.Equals(code, "osd", StringComparison.OrdinalIgnoreCase))
            .OrderBy(code => code, StringComparer.Ordinal)
            .ToArray();

        return Task.FromResult<IReadOnlyList<string>>(languages);
    }

    private TesseractEngine CreateEngine(string tessDataPath, string languageCode) =>
        new(tessDataPath, languageCode, EngineMode.Default);

    private string ResolveTessDataPath()
    {
        if (!string.IsNullOrWhiteSpace(_options.TessDataPath))
        {
            return Path.IsPathRooted(_options.TessDataPath)
                ? _options.TessDataPath
                : Path.GetFullPath(Path.Combine(_contentRoot, _options.TessDataPath));
        }

        return Path.Combine(_contentRoot, "App_Data", "tessdata");
    }

    private static string NormalizeLanguage(string? language)
    {
        if (!string.IsNullOrWhiteSpace(language))
        {
            return language!.Trim();
        }

        // Fall back to configured default and drop unsupported whitespace.
        return string.Join("+", new[] { "fas", "eng" });
    }

    private static string TesseractErrorMessage(TesseractException ex)
    {
        return string.IsNullOrWhiteSpace(ex.Message)
            ? "موتور OCR قادر به پردازش تصویر نبود."
            : $"موتور OCR با خطا مواجه شد: {ex.Message}";
    }
}
