using Microsoft.Extensions.Logging;
using OfficeAutomation.Modules.Persistence.Entities;

namespace OfficeAutomation.Modules.MowadiyanIntegration;

/// <summary>
/// Sandbox implementation of <see cref="IMowadiyanApiClient"/> used for offline
/// development. Simulates the real Tax Portal API responses, including the
/// generation of a Tax Reference ID (شماره پیگیری مالیاتی) and a fiscal ID
/// (شناسه مالیاتی), without any network traffic.
/// </summary>
public sealed class SandboxMowadiyanApiClient : IMowadiyanApiClient
{
    private readonly ILogger<SandboxMowadiyanApiClient> _logger;

    public SandboxMowadiyanApiClient(ILogger<SandboxMowadiyanApiClient> logger)
    {
        _logger = logger;
    }

    public Task<MowadiyanTokenResponse> GetTokenAsync(
        MowadiyanSettings settings, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Sandbox Mowadiyan token issued (offline).");
        return Task.FromResult(new MowadiyanTokenResponse
        {
            Token = $"sandbox-token-{Guid.NewGuid():N}",
            ExpiresInSeconds = 3600
        });
    }

    public Task<MowadiyanSubmitResult> SubmitInvoiceAsync(
        MowadiyanSettings settings,
        string invoiceJson,
        string? pkcs7Envelope,
        CancellationToken cancellationToken)
    {
        // Simulate a deterministic fiscal id derived from the payload + economic code.
        var seed = $"{settings.EconomicCode}|{invoiceJson.GetHashCode():X}";
        var fiscalId = $"00{seed.GetHashCode() & 0x7fffffff:X10}";

        // Tax Reference ID (شماره پیگیری مالیاتی) — 16-character numeric tracking id.
        var referenceNumber = BuildTaxReference(settings.EconomicCode);

        _logger.LogInformation(
            "Sandbox Mowadiyan submit accepted. FiscalId={FiscalId}, Reference={Reference}",
            fiscalId, referenceNumber);

        var result = new MowadiyanSubmitResult
        {
            Success = true,
            TaxReferenceId = referenceNumber,
            FiscalId = fiscalId,
            ResponseJson = $"{{\"result\":{{\"fiscalId\":\"{fiscalId}\",\"referenceNumber\":\"{referenceNumber}\"}}}}"
        };

        return Task.FromResult(result);
    }

    public Task<MowadiyanStatusResult> InquireStatusAsync(
        MowadiyanSettings settings,
        MowadiyanStatusRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Sandbox Mowadiyan status inquiry (offline).");
        return Task.FromResult(new MowadiyanStatusResult
        {
            Success = true,
            Status = "SUCCESS",
            TaxReferenceId = request.TaxReferenceId,
            FiscalId = request.FiscalId,
            ResponseJson = "{\"status\":\"SUCCESS\"}"
        });
    }

    /// <summary>
    /// Builds a realistic 16-digit Tax Reference ID (شماره پیگیری مالیاتی).
    /// </summary>
    private static string BuildTaxReference(string economicCode)
    {
        var random = new Random();
        var sb = new System.Text.StringBuilder(16);
        sb.Append("1403"); // fiscal year prefix

        // Deterministic-ish segment derived from the economic code.
        var codeSeed = Math.Abs((economicCode ?? "000").GetHashCode() % 10000);
        sb.Append(codeSeed.ToString("D4"));

        for (var i = 0; i < 8; i++)
        {
            sb.Append(random.Next(0, 10));
        }

        return sb.ToString();
    }
}