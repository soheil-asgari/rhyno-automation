using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace OfficeAutomation.Services.Security;

/// <summary>
/// احراز هویت مبتنی بر API Key برای دسترسی به API عمومی
/// کلید فقط از هدر X-Api-Key پذیرفته می‌شود (نه از Query String).
/// در محیط‌های غیر Development، اگر هیچ کلیدی پیکربندی نشده باشد، درخواست رد می‌شود.
/// </summary>
public sealed class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
    private const string ApiKeyHeaderName = "X-Api-Key";

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Extract API key ONLY from the header — never from the query string,
        // which would leak secrets into logs, browser history and analytics.
        if (!Request.Headers.TryGetValue(ApiKeyHeaderName, out var headerValues))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var apiKey = headerValues.FirstOrDefault();

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        // Validate API key
        if (!TryResolveClient(apiKey, out var clientId, out var clientName))
        {
            Logger.LogWarning("Invalid API key attempt from {RemoteIp}", Context.Connection.RemoteIpAddress);
            return Task.FromResult(AuthenticateResult.Fail("کلید API نامعتبر است."));
        }

        // Create a distinct claims identity per API client so that authorization
        // and audit logging can attribute actions to a specific consumer.
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, clientId),
            new Claim(ClaimTypes.Name, clientName),
            new Claim(ClaimTypes.Role, "ApiAccess"),
            new Claim("auth_method", "apikey"),
            new Claim("api_client_id", clientId),
            new Claim("api_key_hash", ComputeHash(apiKey))
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        Logger.LogInformation("API key authenticated for client '{ClientId}' from {RemoteIp}", clientId, Context.Connection.RemoteIpAddress);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    /// <summary>
    /// Resolves the configured API keys into a stable per-client identity.
    /// Keys are matched against the stored hash (defense in depth), and the
    /// principal carries the client id/name for audit attribution.
    /// </summary>
    private bool TryResolveClient(string apiKey, out string clientId, out string clientName)
    {
        clientId = "api-client";
        clientName = "API Client";

        var validKeys = Options.ValidApiKeys ?? Array.Empty<string>();

        if (validKeys.Length == 0)
        {
            // Fail safely when no keys are configured. In a shared/development
            // deployment without an explicitly configured key we must NOT accept
            // an arbitrary key — this would create an auth bypass.
            Logger.LogWarning("No API keys configured. Rejecting API key authentication (fail-safe).");
            return false;
        }

        // Normalize the configured keys to a lookup of hash -> display name.
        var apiKeyHash = ComputeHash(apiKey);
        foreach (var entry in validKeys)
        {
            var (isMatch, displayName) = MatchKey(entry, apiKey, apiKeyHash);
            if (isMatch)
            {
                clientId = displayName;
                clientName = displayName;
                return true;
            }
        }

        return false;
    }

    private static (bool IsMatch, string DisplayName) MatchKey(string configuredKey, string suppliedKey, string suppliedHash)
    {
        // Configured keys may be provided as:
        //   - a raw key "k1"
        //   - a "name:key" entry "my-client:abc123..."
        string name = "api-client";
        string secret = configuredKey;

        var colonIndex = configuredKey.IndexOf(':');
        if (colonIndex > 0 && colonIndex < configuredKey.Length - 1)
        {
            name = configuredKey[..colonIndex];
            secret = configuredKey[(colonIndex + 1)..];
        }

        var exact = string.Equals(secret, suppliedKey, StringComparison.Ordinal);
        var hash = string.Equals(ComputeHash(secret), suppliedHash, StringComparison.OrdinalIgnoreCase);
        return (exact || hash, name);
    }

    private static string ComputeHash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.ContentType = "application/json";

        var result = System.Text.Json.JsonSerializer.Serialize(new
        {
            success = false,
            message = "احراز هویت الزامی است. لطفاً API Key معتبر در هدر X-Api-Key ارائه دهید.",
            documentation = "/swagger"
        });

        return Response.WriteAsync(result);
    }

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        Response.ContentType = "application/json";

        var result = System.Text.Json.JsonSerializer.Serialize(new
        {
            success = false,
            message = "شما مجوز دسترسی به این منبع را ندارید.",
            documentation = "/swagger"
        });

        return Response.WriteAsync(result);
    }
}

/// <summary>
/// تنظیمات احراز هویت API Key
/// </summary>
public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    /// <summary>
    /// کلیدهای معتبر API. هر ورودی می‌تواند به صورت raw ("secret") یا
    /// "clientName:secret" باشد تا هویت هر کلاینت مشخص شود.
    /// در محیط Production از متغیر محیطی/Key Vault خوانده شود.
    /// </summary>
    public string[]? ValidApiKeys { get; set; }
}

/// <summary>
/// Extension methods for registering API Key authentication
/// </summary>
public static class ApiKeyAuthenticationExtensions
{
    public const string AuthenticationScheme = "ApiKey";

    public static AuthenticationBuilder AddApiKeyAuthentication(
        this AuthenticationBuilder builder,
        Action<ApiKeyAuthenticationOptions>? configureOptions = null)
    {
        return builder.AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(
            AuthenticationScheme,
            "API Key",
            options =>
            {
                configureOptions?.Invoke(options);
            });
    }
}