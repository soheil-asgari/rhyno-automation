using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace OfficeAutomation.Services.Licensing;

/// <summary>
/// Background hosted service that periodically performs heartbeat checks with the remote license server.
/// Runs only when the active license has OnlineCheckRequired == true.
/// Built defensively so it does not fail or lock the app when the license server is unconfigured or unreachable.
/// </summary>
public sealed class LicenseHeartbeatService : BackgroundService
{
    private readonly ILicenseContext _licenseContext;
    private readonly IHardwareFingerprintService _fingerprintService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<LicensingOptions> _options;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<LicenseHeartbeatService> _logger;

    private bool _hasLoggedUnconfiguredUrl = false;

    public LicenseHeartbeatService(
        ILicenseContext licenseContext,
        IHardwareFingerprintService fingerprintService,
        IHttpClientFactory httpClientFactory,
        IOptions<LicensingOptions> options,
        IServiceProvider serviceProvider,
        ILogger<LicenseHeartbeatService> logger)
    {
        _licenseContext = licenseContext;
        _fingerprintService = fingerprintService;
        _httpClientFactory = httpClientFactory;
        _options = options;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var opts = _options.Value ?? new LicensingOptions();
        var startupDelay = opts.InitialStartupDelaySeconds > 0
            ? TimeSpan.FromSeconds(opts.InitialStartupDelaySeconds)
            : TimeSpan.FromSeconds(30);

        // Initial startup delay before the first heartbeat attempt
        try
        {
            await Task.Delay(startupDelay, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PerformHeartbeatRoundAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Unexpected error occurred during license heartbeat round.");
            }

            var intervalHours = _options.Value?.HeartbeatIntervalHours > 0
                ? _options.Value.HeartbeatIntervalHours
                : 24;
            var interval = TimeSpan.FromHours(intervalHours);

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    /// <summary>
    /// Executes a single round of the heartbeat check.
    /// Publicly callable for testing and manual triggering.
    /// </summary>
    public async Task PerformHeartbeatRoundAsync(CancellationToken cancellationToken = default)
    {
        var license = _licenseContext.CurrentLicense;

        // 1. Only runs at all if the currently active license has OnlineCheckRequired == true.
        // If false or no license loaded, effectively no-op.
        if (license == null || !license.OnlineCheckRequired)
        {
            _logger.LogDebug("License heartbeat skipped: license is null or OnlineCheckRequired is false.");
            return;
        }

        // Check if already revoked
        if (_licenseContext.ValidationResult.FailureReason == LicenseValidationFailureReason.Revoked)
        {
            _logger.LogDebug("License heartbeat skipped: license is already marked as Revoked.");
            return;
        }

        var opts = _options.Value ?? new LicensingOptions();
        var heartbeatUrl = opts.HeartbeatUrl?.Trim();

        // 2. If the HeartbeatUrl is empty/unconfigured, treat this exactly like "the server was unreachable".
        // Do NOT treat unconfigured-URL as an error state or log warning spam; log once at Info level.
        if (string.IsNullOrWhiteSpace(heartbeatUrl))
        {
            if (!_hasLoggedUnconfiguredUrl)
            {
                _logger.LogInformation("License heartbeat URL is not configured. Running offline within configured grace period ({GraceDays} days).", opts.HeartbeatGraceDays);
                _hasLoggedUnconfiguredUrl = true;
            }

            await _licenseContext.UpdateHeartbeatStateAsync(state =>
            {
                state.LastAttemptAtUtc = DateTime.UtcNow;
                state.LastStatus = "unreachable";
                state.LastErrorMessage = "Heartbeat URL is not configured.";
            }, cancellationToken);

            return;
        }

        // 3. POST { lic_id, fingerprint, current_user_counts } to configured heartbeat URL
        Dictionary<string, int>? currentUserCounts = null;
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var enforcementService = scope.ServiceProvider.GetService<ILicenseUserEnforcementService>();
            if (enforcementService != null)
            {
                var usages = await enforcementService.GetModuleUserUsagesAsync(cancellationToken);
                var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (var usage in usages)
                {
                    if (!string.Equals(usage.ModuleName, "Global", StringComparison.OrdinalIgnoreCase))
                    {
                        counts[usage.ModuleName] = usage.ActiveUserCount;
                    }
                }
                currentUserCounts = counts;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to calculate current user counts for license heartbeat payload.");
        }

        var requestPayload = new HeartbeatRequest
        {
            LicenseId = license.LicenseId,
            Fingerprint = _fingerprintService.GetFingerprint(),
            CurrentUserCounts = currentUserCounts
        };

        var json = JsonSerializer.Serialize(requestPayload);
        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, heartbeatUrl)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        if (!string.IsNullOrWhiteSpace(opts.ApiKey))
        {
            requestMessage.Headers.Add("X-Api-Key", opts.ApiKey.Trim());
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", opts.ApiKey.Trim());
        }

        HttpResponseMessage response;
        try
        {
            var httpClient = _httpClientFactory.CreateClient("LicenseHeartbeatClient");
            httpClient.Timeout = TimeSpan.FromSeconds(15);
            response = await httpClient.SendAsync(requestMessage, cancellationToken);
        }
        catch (Exception ex)
        {
            // Server unreachable (timeout, DNS, connection refused, network down)
            _logger.LogInformation("License heartbeat server unreachable at {Url}: {Error}. App continuing under grace period ({Days} days remaining).",
                heartbeatUrl, ex.Message, _licenseContext.GraceDaysRemaining);

            await _licenseContext.UpdateHeartbeatStateAsync(state =>
            {
                state.LastAttemptAtUtc = DateTime.UtcNow;
                state.LastStatus = "unreachable";
                state.LastErrorMessage = ex.Message;
            }, cancellationToken);

            return;
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogInformation("License heartbeat returned non-success status code {StatusCode} from {Url}. Treating as unreachable.",
                (int)response.StatusCode, heartbeatUrl);

            await _licenseContext.UpdateHeartbeatStateAsync(state =>
            {
                state.LastAttemptAtUtc = DateTime.UtcNow;
                state.LastStatus = "unreachable";
                state.LastErrorMessage = $"HTTP {(int)response.StatusCode}";
            }, cancellationToken);

            return;
        }

        // Parse response
        HeartbeatResponse? result = null;
        try
        {
            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            result = JsonSerializer.Deserialize<HeartbeatResponse>(responseJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse heartbeat response from {Url}.", heartbeatUrl);
            await _licenseContext.UpdateHeartbeatStateAsync(state =>
            {
                state.LastAttemptAtUtc = DateTime.UtcNow;
                state.LastStatus = "unreachable";
                state.LastErrorMessage = "Invalid response format";
            }, cancellationToken);
            return;
        }

        if (result == null || string.IsNullOrWhiteSpace(result.Status))
        {
            _logger.LogWarning("Heartbeat response from {Url} had empty status.", heartbeatUrl);
            return;
        }

        var status = result.Status.Trim().ToLowerInvariant();

        switch (status)
        {
            case "active":
                _logger.LogInformation("Heartbeat check succeeded. License status: active.");
                await _licenseContext.UpdateHeartbeatStateAsync(state =>
                {
                    state.LastSuccessfulHeartbeatAt = DateTime.UtcNow;
                    state.LastAttemptAtUtc = DateTime.UtcNow;
                    state.LastStatus = "active";
                    state.LastErrorMessage = null;
                    state.LastLicenseId = license.LicenseId;
                }, cancellationToken);
                break;

            case "updated":
                _logger.LogInformation("Heartbeat received updated license file from server. Activating new license automatically.");
                if (!string.IsNullOrWhiteSpace(result.UpdatedLicenseFile))
                {
                    var fileContent = result.UpdatedLicenseFile.Trim();
                    // Try decoding if it was base64 encoded
                    try
                    {
                        var bytes = Convert.FromBase64String(fileContent);
                        var decoded = Encoding.UTF8.GetString(bytes);
                        if (decoded.Split('.').Length == 3) // looks like a JWT
                        {
                            fileContent = decoded;
                        }
                    }
                    catch
                    {
                        // Was already plain text / raw JWT
                    }

                    var activation = await _licenseContext.ActivateLicenseAsync(fileContent, cancellationToken);
                    if (activation.IsValid)
                    {
                        _logger.LogInformation("New license file activated seamlessly via heartbeat update.");
                        await _licenseContext.UpdateHeartbeatStateAsync(state =>
                        {
                            state.LastSuccessfulHeartbeatAt = DateTime.UtcNow;
                            state.LastAttemptAtUtc = DateTime.UtcNow;
                            state.LastStatus = "active";
                            state.LastErrorMessage = null;
                            state.LastLicenseId = activation.License?.LicenseId ?? license.LicenseId;
                        }, cancellationToken);
                    }
                    else
                    {
                        _logger.LogWarning("Updated license file failed validation: {Reason} - {Error}", activation.FailureReason, activation.ErrorMessage);
                    }
                }
                else
                {
                    _logger.LogWarning("Heartbeat status was 'updated' but updatedLicenseFile was missing or empty.");
                }
                break;

            case "revoked":
                _logger.LogCritical("Heartbeat response returned 'revoked'. Locking application immediately.");
                _licenseContext.MarkRevoked("لایسنس نرم‌افزار توسط شرکت ارائه‌دهنده ابطال شده است (Revoked) — لطفاً با پشتیبانی تماس بگیرید.");
                break;

            default:
                _logger.LogWarning("Unknown heartbeat status received: '{Status}'.", result.Status);
                break;
        }
    }
}
