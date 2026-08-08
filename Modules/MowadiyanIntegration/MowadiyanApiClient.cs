using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OfficeAutomation.Modules.Persistence.Entities;

namespace OfficeAutomation.Modules.MowadiyanIntegration;

/// <summary>
/// HTTP client for the Mowadiyan (سامانه مودیان) Tax Portal API. Responsible for
/// requesting a token, submitting invoices, and inquiring submission status.
/// </summary>
public interface IMowadiyanApiClient
{
    /// <summary>Requests a portal access token using the configured client credentials.</summary>
    Task<MowadiyanTokenResponse> GetTokenAsync(MowadiyanSettings settings, CancellationToken cancellationToken);

    /// <summary>Submits a single invoice payload to the portal.</summary>
    Task<MowadiyanSubmitResult> SubmitInvoiceAsync(
        MowadiyanSettings settings,
        string invoiceJson,
        string? pkcs7Envelope,
        CancellationToken cancellationToken);

    /// <summary>Inquires the submission status of an invoice.</summary>
    Task<MowadiyanStatusResult> InquireStatusAsync(
        MowadiyanSettings settings,
        MowadiyanStatusRequest request,
        CancellationToken cancellationToken);
}

/// <inheritdoc />
public class MowadiyanApiClient : IMowadiyanApiClient
{
    private readonly HttpClient _httpClient;
    private readonly IMowadiyanCryptoService _cryptoService;
    private readonly IOptions<MowadiyanOptions> _options;
    private readonly ILogger<MowadiyanApiClient> _logger;

    public MowadiyanApiClient(
        HttpClient httpClient,
        IMowadiyanCryptoService cryptoService,
        IOptions<MowadiyanOptions> options,
        ILogger<MowadiyanApiClient> logger)
    {
        _httpClient = httpClient;
        _cryptoService = cryptoService;
        _options = options;
        _logger = logger;
    }

    public async Task<MowadiyanTokenResponse> GetTokenAsync(
        MowadiyanSettings settings, CancellationToken cancellationToken)
    {
        var baseUrl = NormalizeBaseUrl(settings);

        var body = new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = settings.ClientId,
            ["client_secret"] = settings.MemorySecret ?? string.Empty
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, baseUrl + "/api/v1/token")
        {
            Content = new FormUrlEncodedContent(body)
        };

        var response = await _httpClient.SendAsync(request, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Mowadiyan token request failed: {Status} {Body}",
                response.StatusCode, json);
            return new MowadiyanTokenResponse
            {
                Error = ((int)response.StatusCode).ToString(),
                ErrorDescription = json
            };
        }

        return JsonSerializer.Deserialize<MowadiyanTokenResponse>(json)
               ?? new MowadiyanTokenResponse();
    }
public async Task<MowadiyanSubmitResult> SubmitInvoiceAsync(
        MowadiyanSettings settings,
        string invoiceJson,
        string? pkcs7Envelope,
        CancellationToken cancellationToken)
    {
        var baseUrl = NormalizeBaseUrl(settings);
        var taxpayerSegment = string.IsNullOrWhiteSpace(settings.EconomicCode)
            ? "taxpayer"
            : $"taxpayer/{Uri.EscapeDataString(settings.EconomicCode)}";

        var path = $"/api/v1/{taxpayerSegment}/invoices";
        var timestamp = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");
        var traceNumber = Guid.NewGuid().ToString("N");

        var payloadBytes = Encoding.UTF8.GetBytes(invoiceJson);
        var signature = _cryptoService.CreateSignatureHeaders(
            "POST", path, payloadBytes, timestamp, traceNumber, settings.ClientId, settings);

        var submit = new MowadiyanSubmitRequest
        {
            InvoiceJson = invoiceJson,
            Pkcs7Envelope = pkcs7Envelope,
            Signature = signature.Signature,
            SignatureKeyId = signature.SignatureKeyId,
            SignatureAlgorithm = signature.SignatureAlgorithm,
            SignatureHeaders = signature.SignatureHeaders,
            Timestamp = timestamp,
            TraceNumber = traceNumber,
            ClientId = settings.ClientId
        };

        var json = JsonSerializer.Serialize(submit);
        using var request = new HttpRequestMessage(HttpMethod.Post, baseUrl + path)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        ApplyPortalHeaders(request, signature, timestamp, traceNumber);

        var response = await _httpClient.SendAsync(request, cancellationToken);
        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Mowadiyan submit failed: {Status} {Body}",
                response.StatusCode, responseJson);
            return new MowadiyanSubmitResult
            {
                Success = false,
                ResponseJson = responseJson,
                ErrorMessage = $"خطا در ارسال به سامانه مودیان: {(int)response.StatusCode}"
            };
        }

        return ParseSubmitResponse(responseJson);
    }

    public async Task<MowadiyanStatusResult> InquireStatusAsync(
        MowadiyanSettings settings,
        MowadiyanStatusRequest request,
        CancellationToken cancellationToken)
    {
        var baseUrl = NormalizeBaseUrl(settings);
        var path = "/api/v1/inquiry/v2";

        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(request.FiscalId))
        {
            query.Add($"fiscalId={Uri.EscapeDataString(request.FiscalId)}");
        }
        if (!string.IsNullOrWhiteSpace(request.TaxReferenceId))
        {
            query.Add($"referenceNumber={Uri.EscapeDataString(request.TaxReferenceId)}");
        }

        var fullPath = query.Count > 0 ? path + "?" + string.Join("&", query) : path;
        var timestamp = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");
        var traceNumber = Guid.NewGuid().ToString("N");

        var signature = _cryptoService.CreateSignatureHeaders(
            "GET", fullPath, Array.Empty<byte>(), timestamp, traceNumber, settings.ClientId, settings);

        using var requestMessage = new HttpRequestMessage(HttpMethod.Get, baseUrl + fullPath);
        ApplyPortalHeaders(requestMessage, signature, timestamp, traceNumber);

        var response = await _httpClient.SendAsync(requestMessage, cancellationToken);
        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Mowadiyan inquiry failed: {Status} {Body}",
                response.StatusCode, responseJson);
            return new MowadiyanStatusResult
            {
                Success = false,
                ResponseJson = responseJson,
                ErrorMessage = $"خطا در استعلام وضعیت: {(int)response.StatusCode}"
            };
        }

        return new MowadiyanStatusResult
        {
            Success = true,
            Status = "SUCCESS",
            TaxReferenceId = request.TaxReferenceId,
            FiscalId = request.FiscalId,
            ResponseJson = responseJson
        };
    }
private void ApplyPortalHeaders(
        HttpRequestMessage request,
        MowadiyanSignatureResult signature,
        string timestamp,
        string traceNumber)
    {
        request.Headers.TryAddWithoutValidation("signature", signature.Signature);
        request.Headers.TryAddWithoutValidation("signature-key-id", signature.SignatureKeyId);
        request.Headers.TryAddWithoutValidation("signature-algorithm", signature.SignatureAlgorithm);
        request.Headers.TryAddWithoutValidation("signature-headers", signature.SignatureHeaders);
        request.Headers.TryAddWithoutValidation("timestamp", timestamp);
        request.Headers.TryAddWithoutValidation("trace-number", traceNumber);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    private static MowadiyanSubmitResult ParseSubmitResponse(string responseJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseJson);
            var root = doc.RootElement;

            var result = new MowadiyanSubmitResult
            {
                Success = true,
                ResponseJson = responseJson
            };

            if (root.TryGetProperty("errorMessage", out var err))
            {
                result.Success = false;
                result.ErrorMessage = err.GetString();
            }

            if (root.TryGetProperty("result", out var resultEl) &&
                resultEl.TryGetProperty("referenceNumber", out var refEl))
            {
                result.TaxReferenceId = refEl.GetString();
            }

            if (root.TryGetProperty("result", out resultEl) &&
                resultEl.TryGetProperty("fiscalId", out var fiscalEl))
            {
                result.FiscalId = fiscalEl.GetString();
            }

            return result;
        }
        catch (JsonException)
        {
            return new MowadiyanSubmitResult
            {
                Success = true,
                ResponseJson = responseJson
            };
        }
    }

    private static string NormalizeBaseUrl(MowadiyanSettings settings)
    {
        var url = string.IsNullOrWhiteSpace(settings.BaseUrl)
            ? "https://tp.tax.gov.ir"
            : settings.BaseUrl.TrimEnd('/');
        return url;
    }
}