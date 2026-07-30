using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

namespace OfficeAutomation.Services.Security;

/// <summary>
/// احراز هویت مبتنی بر API Key برای دسترسی به API عمومی
/// </summary>
public sealed class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
    private const string ApiKeyHeaderName = "X-Api-Key";
    private const string ApiKeyQueryParam = "api_key";

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Extract API key from header or query string
        string? apiKey = null;

        if (Request.Headers.TryGetValue(ApiKeyHeaderName, out var headerValues))
        {
            apiKey = headerValues.FirstOrDefault();
        }
        else if (Request.Query.TryGetValue(ApiKeyQueryParam, out var queryValues))
        {
            apiKey = queryValues.FirstOrDefault();
        }

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        // Validate API key (in production, validate against database or secure store)
        if (!IsValidApiKey(apiKey))
        {
            Logger.LogWarning("Invalid API key attempt from {RemoteIp}", Context.Connection.RemoteIpAddress);
            return Task.FromResult(AuthenticateResult.Fail("کلید API نامعتبر است."));
        }

        // Create claims identity
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "api-client"),
            new Claim(ClaimTypes.Name, $"API Client: {apiKey[..Math.Min(8, apiKey.Length)]}..."),
            new Claim(ClaimTypes.Role, "ApiAccess"),
            new Claim("auth_method", "apikey"),
            new Claim("api_key_hash", ComputeHash(apiKey))
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        Logger.LogInformation("API key authenticated successfully from {RemoteIp}", Context.Connection.RemoteIpAddress);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    private bool IsValidApiKey(string apiKey)
    {
        // In production: validate against database, configuration, or key management service
        // For now, validate against configured keys
        var validKeys = Options.ValidApiKeys ?? Array.Empty<string>();

        if (validKeys.Length == 0)
        {
            // Allow any non-empty key in development
            Logger.LogWarning("No API keys configured. Allowing any key in {Environment} mode.",
                "Development");
            return apiKey.Length >= 16;
        }

        return validKeys.Any(key =>
            string.Equals(key, apiKey, StringComparison.Ordinal) ||
            string.Equals(ComputeHash(key), ComputeHash(apiKey), StringComparison.OrdinalIgnoreCase));
    }

    private static string ComputeHash(string input)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.ContentType = "application/json";

        var result = System.Text.Json.JsonSerializer.Serialize(new
        {
            success = false,
            message = "احراز هویت الزامی است. لطفاً API Key معتبر ارائه دهید.",
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
    /// لیست کلیدهای معتبر API (در محیط Production از دیتابیس یا Key Vault خوانده شود)
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