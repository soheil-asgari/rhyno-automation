using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OfficeAutomation.Modules.MowadiyanIntegration;
using OfficeAutomation.Utilities;
using Xunit;

namespace OfficeAutomation.Tests;

/// <summary>
/// Unit tests for the refactored Tax Portal exporter (<see cref="TaxExporterService"/>)
/// and its supporting helpers: Persian/Arabic glyph normalization
/// (<see cref="PersianTextNormalizer.NormalizeText"/>) and the external HttpClient CAPTCHA
/// OCR wrapper (<see cref="ExternalCaptchaOcrClient"/>) with its JSON code extraction.
/// </summary>
public class TaxExporterServiceTests
{
    // ── PersianTextNormalizer ────────────────────────────────────────────────────────

    [Fact]
    public void NormalizeText_FoldsPersianAndArabicYehKaf()
    {
        // Arabic yeh (ي U+064A) and kaf (ك U+0643) must be folded to the Persian variants.
        const string input = "درخواست فعال خروجي";      // contains U+064A
        const string expected = "درخواست فعال خروجی";    // canonical U+06CC

        var normalized = PersianTextNormalizer.NormalizeText(input);
        Assert.Equal(expected, normalized);
    }

    [Fact]
    public void NormalizeText_RemovesZwnjZwjAndTatweel()
    {
        const string input = "\u06A9\u0640\u200C\u200D\u0647"; // ک + tatweel + ZWNJ + ZWJ + ه
        const string expected = "که";

        Assert.Equal(expected, PersianTextNormalizer.NormalizeText(input));
    }

    [Fact]
    public void NormalizeText_FoldsIndicDigits()
    {
        // Persian-Indic ۰..۹ and Arabic-Indic ٠..٩ -> ASCII 0..9.
        const string input = "۰۱۲۳٤٥٦٧٨٩";
        const string expected = "0123456789";

        Assert.Equal(expected, PersianTextNormalizer.NormalizeText(input));
    }

    // ── ExternalCaptchaOcrClient ─────────────────────────────────────────────────────

    [Fact]
    public async Task OcrClient_ReturnsEmpty_WhenEndpointNotConfigured()
    {
        var client = BuildClient(new CaptchaOcrOptions { OcrBaseUrl = string.Empty });
        var result = await client.SolveAsync(new byte[] { 1, 2, 3 }, CancellationToken.None);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public async Task OcrClient_ExtractsCode_FromJsonResponse()
    {
        var handler = new StubHttpHandler(HttpStatusCode.OK, "{\"code\":\"ab12cd\"}");
        var client = BuildClient(new CaptchaOcrOptions { OcrBaseUrl = "http://ocr.test/solve" }, handler);

        var result = await client.SolveAsync(new byte[] { 137, 80, 78, 71 }, CancellationToken.None);

        Assert.Equal("ab12cd", result);
        Assert.NotNull(handler.LastRequestBody);
        Assert.Contains("captcha", handler.LastRequestBody ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OcrClient_ReturnsEmpty_OnNonSuccessResponse()
    {
        var handler = new StubHttpHandler(HttpStatusCode.InternalServerError, "oops");
        var client = BuildClient(new CaptchaOcrOptions { OcrBaseUrl = "http://ocr.test/solve" }, handler);

        var result = await client.SolveAsync(new byte[] { 1, 2, 3 }, CancellationToken.None);

        Assert.Equal(string.Empty, result);
    }

    // ── helpers ──────────────────────────────────────────────────────────────────────

    private static ExternalCaptchaOcrClient BuildClient(CaptchaOcrOptions options, HttpMessageHandler? handler = null)
    {
        var httpClient = handler is null
            ? new HttpClient()
            : new HttpClient(handler);
        return new ExternalCaptchaOcrClient(
            httpClient,
            Options.Create(options),
            NullLogger<ExternalCaptchaOcrClient>.Instance);
    }

    private sealed class StubHttpHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _body;

        public string? LastRequestBody { get; private set; }

        public StubHttpHandler(HttpStatusCode status, string body)
        {
            _status = status;
            _body = body;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestBody = request.Content?.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult();
            var response = new HttpResponseMessage(_status)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }
}
