using System.Reflection;
using System.Resources;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace OfficeAutomation.Modules.MowadiyanIntegration;

/// <summary>
/// In-process ddddocr CAPTCHA solver.
///
/// Runs the exact same <c>common_old.onnx</c> classification model that the reference
/// Python implementation uses, but directly inside the .NET process via
/// <see cref="InferenceSession"/> (Microsoft.ML.OnnxRuntime) — no Python sidecar, no
/// external HTTP OCR service, no separate process.
///
/// The model + its character set are obtained from the embedded resources of the
/// <c>DdddOCR</c> NuGet package (which already carries them, so nothing extra is
/// downloaded) and materialized once to <c>App_Data/captcha</c>, matching
/// <see cref="MowadiyanOptions.CaptchaModelPath"/>.
///
/// The image pre-processing + greedy CTC decoding is implemented here (with the app's
/// own SixLabors.ImageSharp 3.x) because the <c>DdddOCR.OCRCore</c> wrapper shipped in
/// that package is binary-incompatible with ImageSharp 3.x and throws at runtime.
/// The resulting text is validated to match the reference Python ddddocr output on the
/// same input.
/// </summary>
public sealed class DdddCaptchaOcrClient : ICaptchaOcrClient
{
    private const string DdddAssemblyName = "DdddOCR";
    private const string ResourceFileName = "DdddOCR.Properties.Resources.resources";
    private const string ModelResourceName = "Common_Old";
    private const string CharsetResourceName = "CharSet";

    /// <summary>The classification model expects a fixed grayscale height of 64.</summary>
    private const int FixedHeight = 64;

    /// <summary>Model input image width must be an integer multiple of 64.</summary>
    private const int WidthAlignment = 64;

    private readonly IOptions<MowadiyanOptions> _options;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<DdddCaptchaOcrClient> _logger;
    private readonly ICaptchaOcrClient? _externalFallback;

    private readonly object _gate = new();
    /// <inheritdoc />
    public async Task<string> SolveAsync(byte[] imageBytes, CancellationToken cancellationToken)
    {
        if (imageBytes is null || imageBytes.Length == 0)
        {
            return string.Empty;
        }

        try
        {
            EnsureModelReady();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "In-process ddddocr model could not be prepared: {Message}", ex.Message);
            return await FallbackAsync(imageBytes, cancellationToken);
        }

        try
        {
            using var session = GetOrCreateSession();
            if (session is null || _charset is null)
            {
                return await FallbackAsync(imageBytes, cancellationToken);
            }

            var tensor = Preprocess(imageBytes);
            if (tensor is null)
            {
                return string.Empty;
            }

            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("input1", tensor)
            };
            using var results = session.Run(inputs);
            var output = results.First().AsTensor<long>();
            if (output is null)
            {
                return string.Empty;
            }

            var code = DecodeGreedyOcr(output, _charset);
            _logger.LogInformation("ddddocr (in-process) recognised CAPTCHA: {Code}", code);
            if (!string.IsNullOrWhiteSpace(code))
            {
                return code;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "In-process ddddocr inference failed: {Message}", ex.Message);
        }

        return await FallbackAsync(imageBytes, cancellationToken);
    }

    /// <summary>True once the ONNX session and character set are loaded.</summary>
    internal bool IsModelReady => _session is not null && _charset is not null;

    private async Task<string> FallbackAsync(byte[] imageBytes, CancellationToken cancellationToken)
    {
        if (_externalFallback is not null)
        {
            return await _externalFallback.SolveAsync(imageBytes, cancellationToken);
        }

        return string.Empty;
    }

    private InferenceSession? _session;
    private string? _charset;
    private string? _modelPath;

    public DdddCaptchaOcrClient(
        IOptions<MowadiyanOptions> options,
        IHostEnvironment environment,
        ILogger<DdddCaptchaOcrClient> logger,
        ICaptchaOcrClient? externalFallback = null)
    {
        _options = options;
        _environment = environment;
        _logger = logger;
        _externalFallback = externalFallback;
    }

    private void EnsureModelReady()
    {
        if (_session is not null && _charset is not null)
        {
            return;
        }

        lock (_gate)
        {
            if (_session is not null && _charset is not null)
            {
                return;
            }

            var contentRoot = _environment.ContentRootPath;
            _modelPath = ResolveModelPath(contentRoot);
            _charset = LoadOrMaterializeCharset(contentRoot, _modelPath);
            _session = new InferenceSession(_modelPath);
        }
    }

    private string ResolveModelPath(string contentRoot)
    {
        var configured = _options.Value.CaptchaModelPath;
        if (string.IsNullOrWhiteSpace(configured))
        {
            return Path.Combine(contentRoot, "App_Data", "captcha", "common_old.onnx");
        }

        return Path.IsPathRooted(configured)
            ? configured
            : Path.Combine(contentRoot, configured);
    }

    private string LoadOrMaterializeCharset(string contentRoot, string modelPath)
    {
        var modelDir = Path.GetDirectoryName(modelPath)
                       ?? Path.Combine(contentRoot, "App_Data", "captcha");
        var charsetPath = Path.Combine(modelDir, "charset.txt");

        // Reuse the materialized charset if the model already lives on disk so warm
        // starts do not have to load the DdddOCR assembly again.
        if (File.Exists(modelPath) && File.Exists(charsetPath))
        {
            return File.ReadAllText(charsetPath, Encoding.UTF8);
        }

        Directory.CreateDirectory(modelDir);
        var (modelBytes, charset) = LoadEmbeddedResources();
        File.WriteAllBytes(modelPath, modelBytes);
        File.WriteAllText(charsetPath, charset, Encoding.UTF8);
        _logger.LogInformation("Materialised ddddocr model ({Bytes} bytes) to {Path}", modelBytes.Length, modelPath);

        return charset;
    }

    private static (byte[] Model, string Charset) LoadEmbeddedResources()
    {
        Assembly assembly;
        try
        {
            assembly = Assembly.Load(DdddAssemblyName);
        }
        catch (Exception)
        {
            throw new InvalidOperationException(
                $"پکیج {DdddAssemblyName} یافت نشد. لطفاً ارجاع NuGet بسته DdddOCR را حفظ کنید.");
        }

        using var stream = assembly.GetManifestResourceStream(ResourceFileName)
            ?? throw new InvalidOperationException(
                $"منبع جاسازی‌شده مدل ({ResourceFileName}) در {DdddAssemblyName} یافت نشد.");

        byte[]? model = null;
        string? charset = null;

        using var reader = new ResourceReader(stream);
        var entries = reader.GetEnumerator();
        while (entries.MoveNext())
        {
            if (string.Equals(entries.Key as string, ModelResourceName, StringComparison.Ordinal))
            {
                model = entries.Value as byte[];
            }
            else if (string.Equals(entries.Key as string, CharsetResourceName, StringComparison.Ordinal))
            {
                charset = entries.Value as string;
            }
        }

        if (model is null || model.Length == 0)
        {
            throw new InvalidOperationException($"مدل {ModelResourceName} از پکیج {DdddAssemblyName} قابل استخراج نبود.");
        }

        if (string.IsNullOrWhiteSpace(charset))
        {
            throw new InvalidOperationException($"جدول نویسه ({CharsetResourceName}) از پکیج {DdddAssemblyName} قابل استخراج نبود.");
        }

        return (model, charset);
    }

    private InferenceSession? GetOrCreateSession()
    {
        var session = _session;
        if (session is not null)
        {
            return session;
        }

        try
        {
            return new InferenceSession(_modelPath!);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not create ONNX session for {Path}", _modelPath);
            return null;
        }
    }

    /// <summary>
    /// Preprocess matching ddddocr: grayscale, resize so height == 64 (keep aspect),
    /// pad width to the next multiple of 64 (white margins), then normalize 0..1.
    /// </summary>
    private static Tensor<float>? Preprocess(byte[] imageBytes)
    {
        try
        {
            using var image = Image.Load<L8>(imageBytes);
            var width = image.Width;
            var height = image.Height;
            if (width <= 0 || height <= 0)
            {
                return null;
            }

            var scale = (double)FixedHeight / height;
            var targetWidth = Math.Max(1, (int)Math.Round(width * scale));
            image.Mutate(x => x.Resize(targetWidth, FixedHeight));

            var paddedWidth = ((targetWidth + WidthAlignment - 1) / WidthAlignment) * WidthAlignment;
            using var padded = new Image<L8>(paddedWidth, FixedHeight, new L8(255));
            padded.Mutate(x => x.DrawImage(image, new Point((paddedWidth - targetWidth) / 2, 0), 1f));

            var tensor = new DenseTensor<float>(new[] { 1, 1, FixedHeight, paddedWidth });
            padded.ProcessPixelRows(accessor =>
            {
                for (var y = 0; y < FixedHeight; y++)
                {
                    var row = accessor.GetRowSpan(y);
                    for (var x = 0; x < paddedWidth; x++)
                    {
                        tensor[0, 0, y, x] = row[x].PackedValue / 255f;
                    }
                }
            });

            return tensor;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>Greedy CTC decoding mirroring ddddocr: collapse repeats &amp; skip blank.</summary>
    internal static string DecodeGreedyOcr(Tensor<long> output, string charset)
    {
        if (output is null || output.Length == 0 || string.IsNullOrEmpty(charset))
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        var previous = -1L;
        for (var i = 0; i < output.Length; i++)
        {
            var index = output[0, i];
            if (index == previous)
            {
                continue; // collapse repeated predictions (CTC)
            }

            previous = index;
            if (index <= 0)
            {
                continue; // blank token / separator
            }

            var charIndex = (int)index - 1;
            if (charIndex < charset.Length)
            {
                sb.Append(charset[charIndex]);
            }
        }

        return CleanCaptcha(sb.ToString());
    }

    /// <summary>Keep only typical CAPTCHA alphanumerics (ASCII letters + digits).</summary>
    internal static string CleanCaptcha(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        return Regex.Replace(raw, "[^A-Za-z0-9]", string.Empty);
    }
}

