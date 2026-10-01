using System.IdentityModel.Tokens.Jwt;
using System.Reflection;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace OfficeAutomation.Services.Licensing;

/// <summary>
/// Verifies RS256 signed license tokens using an embedded public key.
/// </summary>
public interface ILicenseValidator
{
    /// <summary>
    /// Synchronously validates the license JWT token signature, structure, and expiration.
    /// Does not perform hardware fingerprint matching unless explicitly provided.
    /// </summary>
    LicenseValidationResult Validate(string? licenseToken);

    /// <summary>
    /// Asynchronously validates the license file content, checking JWT signature,
    /// expiration date against current UTC time, and comparing against the current server's hardware fingerprint.
    /// </summary>
    Task<LicenseValidationResult> ValidateAsync(string? licenseFileContent, CancellationToken cancellationToken = default);
}

public sealed class LicenseValidator : ILicenseValidator
{
    private const string ExpectedIssuer = "RhynoAutomation-LicenseAuthority";
    private const string ExpectedAudience = "RhynoAutomation-OnPremApp";
    private const string EmbeddedKeyResourceName = "OfficeAutomation.Services.Licensing.license-public.pem";

    private readonly ILogger<LicenseValidator> _logger;
    private readonly RsaSecurityKey _publicSecurityKey;
    private readonly IHardwareFingerprintService? _fingerprintService;

    public LicenseValidator(ILogger<LicenseValidator> logger, IHardwareFingerprintService? fingerprintService = null)
    {
        _logger = logger;
        _fingerprintService = fingerprintService;
        _publicSecurityKey = LoadEmbeddedPublicKey();
    }

    /// <summary>
    /// Constructor overload allowing injection of a specific RSA public key and optional hardware fingerprint service (useful for tests).
    /// </summary>
    public LicenseValidator(RSA rsaPublicKey, ILogger<LicenseValidator> logger, IHardwareFingerprintService? fingerprintService = null)
    {
        _logger = logger;
        _fingerprintService = fingerprintService;
        _publicSecurityKey = new RsaSecurityKey(rsaPublicKey);
    }

    public LicenseValidationResult Validate(string? licenseToken)
    {
        if (string.IsNullOrWhiteSpace(licenseToken))
        {
            return LicenseValidationResult.Failure(LicenseValidationFailureReason.Missing, "License token is missing or empty.");
        }

        licenseToken = licenseToken.Trim();

        var tokenHandler = new JwtSecurityTokenHandler();
        if (!tokenHandler.CanReadToken(licenseToken))
        {
            _logger.LogWarning("Provided license token is not a valid JWT format.");
            return LicenseValidationResult.Failure(LicenseValidationFailureReason.Malformed, "The license token is malformed and cannot be read.");
        }

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = _publicSecurityKey,
            ValidateIssuer = true,
            ValidIssuer = ExpectedIssuer,
            ValidateAudience = true,
            ValidAudience = ExpectedAudience,
            ValidateLifetime = false, // Lifetime validated manually to distinguish Expired from other failures and support perpetual
            RequireSignedTokens = true,
            ValidAlgorithms = new[] { SecurityAlgorithms.RsaSha256 },
            ClockSkew = TimeSpan.FromMinutes(2)
        };

        ClaimsPrincipal principal;
        SecurityToken validatedSecurityToken;

        try
        {
            principal = tokenHandler.ValidateToken(licenseToken, validationParameters, out validatedSecurityToken);
        }
        catch (SecurityTokenInvalidSignatureException ex)
        {
            _logger.LogWarning(ex, "License signature verification failed.");
            return LicenseValidationResult.Failure(LicenseValidationFailureReason.InvalidSignature, "License signature is invalid or has been tampered with.");
        }
        catch (SecurityTokenException ex)
        {
            _logger.LogWarning(ex, "License token security failure: {Message}", ex.Message);
            return LicenseValidationResult.Failure(LicenseValidationFailureReason.Malformed, $"Invalid license token: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error verifying license token.");
            return LicenseValidationResult.Failure(LicenseValidationFailureReason.Malformed, "Failed to parse license token.");
        }

        var jwtToken = (JwtSecurityToken)validatedSecurityToken;

        // Parse claims into LicenseInfo
        try
        {
            var licIdStr = principal.FindFirst("lic_id")?.Value
                ?? (jwtToken.Payload.TryGetValue("lic_id", out var rawLicId) ? rawLicId?.ToString() : null);
            if (!Guid.TryParse(licIdStr, out var licId))
            {
                return LicenseValidationResult.Failure(LicenseValidationFailureReason.Malformed, "License claim 'lic_id' is missing or not a valid GUID.");
            }

            var customerName = principal.FindFirst("customer_name")?.Value
                ?? (jwtToken.Payload.TryGetValue("customer_name", out var rawCustName) ? rawCustName?.ToString() : null)
                ?? string.Empty;

            var customerId = principal.FindFirst("customer_id")?.Value
                ?? (jwtToken.Payload.TryGetValue("customer_id", out var rawCustId) ? rawCustId?.ToString() : null)
                ?? string.Empty;

            var hardwareFingerprint = principal.FindFirst("hardware_fingerprint")?.Value
                ?? (jwtToken.Payload.TryGetValue("hardware_fingerprint", out var rawFp) ? rawFp?.ToString() : null)
                ?? string.Empty;

            var licenseType = principal.FindFirst("license_type")?.Value
                ?? (jwtToken.Payload.TryGetValue("license_type", out var rawLicType) ? rawLicType?.ToString() : null)
                ?? "subscription";

            // Parse max_users: JSON number, integer, long, JsonElement, or string fallback
            int maxUsers = 1;
            if (jwtToken.Payload.TryGetValue("max_users", out var rawMaxUsers))
            {
                if (rawMaxUsers is int i)
                    maxUsers = i;
                else if (rawMaxUsers is long l)
                    maxUsers = (int)l;
                else if (rawMaxUsers is JsonElement elem && elem.ValueKind == JsonValueKind.Number && elem.TryGetInt32(out var parsedNum))
                    maxUsers = parsedNum;
                else if (rawMaxUsers != null && int.TryParse(rawMaxUsers.ToString(), out var parsedStr))
                    maxUsers = parsedStr;
            }
            else
            {
                var maxUsersStr = principal.FindFirst("max_users")?.Value ?? "1";
                int.TryParse(maxUsersStr, out maxUsers);
            }
            if (maxUsers <= 0) maxUsers = 1;

            // Parse is_perpetual: JSON boolean, JsonElement, or string fallback
            bool isPerpetual = false;
            if (jwtToken.Payload.TryGetValue("is_perpetual", out var rawIsPerpetual))
            {
                if (rawIsPerpetual is bool b)
                    isPerpetual = b;
                else if (rawIsPerpetual is JsonElement elem && (elem.ValueKind == JsonValueKind.True || elem.ValueKind == JsonValueKind.False))
                    isPerpetual = elem.GetBoolean();
                else if (rawIsPerpetual != null && bool.TryParse(rawIsPerpetual.ToString(), out var parsedBool))
                    isPerpetual = parsedBool;
            }
            else
            {
                var isPerpetualStr = principal.FindFirst("is_perpetual")?.Value;
                isPerpetual = string.Equals(isPerpetualStr, "true", StringComparison.OrdinalIgnoreCase);
            }

            if (!isPerpetual && string.Equals(licenseType, "perpetual", StringComparison.OrdinalIgnoreCase))
            {
                isPerpetual = true;
            }

            // Parse online_check_required: boolean, JsonElement, or string fallback (default true)
            bool onlineCheckRequired = true;
            if (jwtToken.Payload.TryGetValue("online_check_required", out var rawOnlineCheck))
            {
                if (rawOnlineCheck is bool bOnline)
                    onlineCheckRequired = bOnline;
                else if (rawOnlineCheck is JsonElement elem && (elem.ValueKind == JsonValueKind.True || elem.ValueKind == JsonValueKind.False))
                    onlineCheckRequired = elem.GetBoolean();
                else if (rawOnlineCheck != null && bool.TryParse(rawOnlineCheck.ToString(), out var parsedBool))
                    onlineCheckRequired = parsedBool;
            }
            else
            {
                var onlineCheckStr = principal.FindFirst("online_check_required")?.Value;
                if (!string.IsNullOrWhiteSpace(onlineCheckStr) && bool.TryParse(onlineCheckStr, out var parsedBool))
                {
                    onlineCheckRequired = parsedBool;
                }
            }

            // Modules claim
            var modules = new List<string>();
            if (jwtToken.Payload.TryGetValue("modules", out var rawModules))
            {
                if (rawModules is IEnumerable<string> strList)
                {
                    modules.AddRange(strList.Where(s => !string.IsNullOrEmpty(s)));
                }
                else if (rawModules is IEnumerable<object> objList)
                {
                    modules.AddRange(objList.Select(o => o?.ToString()).Where(s => !string.IsNullOrEmpty(s))!);
                }
                else if (rawModules is JsonElement elem && elem.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in elem.EnumerateArray())
                    {
                        var s = item.GetString();
                        if (!string.IsNullOrEmpty(s)) modules.Add(s);
                    }
                }
                else if (rawModules is string singleStr && !string.IsNullOrWhiteSpace(singleStr))
                {
                    if (singleStr.TrimStart().StartsWith("["))
                    {
                        try
                        {
                            var parsed = JsonSerializer.Deserialize<List<string>>(singleStr);
                            if (parsed != null) modules.AddRange(parsed);
                        }
                        catch
                        {
                            modules.AddRange(singleStr.Trim('[', ']', '"').Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
                        }
                    }
                    else
                    {
                        modules.AddRange(singleStr.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
                    }
                }
            }
            else
            {
                var moduleClaims = principal.FindAll("modules").Select(c => c.Value).ToList();
                if (moduleClaims.Count > 1)
                {
                    modules.AddRange(moduleClaims);
                }
                else if (moduleClaims.Count == 1)
                {
                    var single = moduleClaims[0];
                    if (single.TrimStart().StartsWith("["))
                    {
                        try
                        {
                            var parsed = JsonSerializer.Deserialize<List<string>>(single);
                            if (parsed != null) modules.AddRange(parsed);
                        }
                        catch
                        {
                            modules.AddRange(single.Trim('[', ']', '"').Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
                        }
                    }
                    else
                    {
                        modules.AddRange(single.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
                    }
                }
            }

            // Per-module user limits (nested JSON object claim or dictionary)
            var moduleLimits = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (jwtToken.Payload.TryGetValue("module_user_limits", out var rawLimits))
            {
                if (rawLimits is IDictionary<string, int> dictInt)
                {
                    foreach (var kv in dictInt) moduleLimits[kv.Key] = kv.Value;
                }
                else if (rawLimits is IDictionary<string, object> dictObj)
                {
                    foreach (var kv in dictObj)
                    {
                        if (kv.Value is int valInt) moduleLimits[kv.Key] = valInt;
                        else if (kv.Value is long valLong) moduleLimits[kv.Key] = (int)valLong;
                        else if (int.TryParse(kv.Value?.ToString(), out var parsedVal)) moduleLimits[kv.Key] = parsedVal;
                    }
                }
                else if (rawLimits is JsonElement elem)
                {
                    if (elem.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var prop in elem.EnumerateObject())
                        {
                            if (prop.Value.TryGetInt32(out var val))
                                moduleLimits[prop.Name] = val;
                            else if (int.TryParse(prop.Value.GetString(), out var parsedVal))
                                moduleLimits[prop.Name] = parsedVal;
                        }
                    }
                    else if (elem.ValueKind == JsonValueKind.String)
                    {
                        try
                        {
                            var parsed = JsonSerializer.Deserialize<Dictionary<string, int>>(elem.GetString()!);
                            if (parsed != null)
                            {
                                foreach (var kv in parsed) moduleLimits[kv.Key] = kv.Value;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to parse module_user_limits JSON string.");
                        }
                    }
                }
                else if (rawLimits is string jsonStr && !string.IsNullOrWhiteSpace(jsonStr))
                {
                    try
                    {
                        var parsed = JsonSerializer.Deserialize<Dictionary<string, int>>(jsonStr);
                        if (parsed != null)
                        {
                            foreach (var kv in parsed) moduleLimits[kv.Key] = kv.Value;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to parse module_user_limits JSON string.");
                    }
                }
            }
            else
            {
                var moduleLimitsClaim = principal.FindFirst("module_user_limits")?.Value;
                if (!string.IsNullOrWhiteSpace(moduleLimitsClaim))
                {
                    try
                    {
                        var parsedLimits = JsonSerializer.Deserialize<Dictionary<string, int>>(moduleLimitsClaim);
                        if (parsedLimits != null)
                        {
                            foreach (var kv in parsedLimits)
                            {
                                moduleLimits[kv.Key] = kv.Value;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to parse module_user_limits claim.");
                    }
                }
            }

            var issuedAt = jwtToken.ValidFrom != DateTime.MinValue ? jwtToken.ValidFrom : DateTime.UtcNow;
            DateTime? expiresAt = isPerpetual ? null : (jwtToken.ValidTo != DateTime.MinValue ? jwtToken.ValidTo : null);

            // Expiry Check
            if (!isPerpetual && expiresAt.HasValue && DateTime.UtcNow > expiresAt.Value.Add(TimeSpan.FromMinutes(2)))
            {
                _logger.LogWarning("License expired on {Expiry}.", expiresAt.Value);
                return LicenseValidationResult.Failure(LicenseValidationFailureReason.Expired, $"License expired on {expiresAt.Value:yyyy-MM-dd HH:mm:ss} UTC.");
            }

            // Parse support_expires_at (nullable UTC timestamp)
            DateTime? supportExpiresAt = null;
            if (jwtToken.Payload.TryGetValue("support_expires_at", out var rawSupportExpires))
            {
                if (rawSupportExpires is DateTime dt)
                {
                    supportExpiresAt = dt.ToUniversalTime();
                }
                else if (rawSupportExpires is JsonElement elem && elem.ValueKind == JsonValueKind.String && DateTime.TryParse(elem.GetString(), null, System.Globalization.DateTimeStyles.AdjustToUniversal, out var parsedDt))
                {
                    supportExpiresAt = parsedDt;
                }
                else if (rawSupportExpires != null && DateTime.TryParse(rawSupportExpires.ToString(), null, System.Globalization.DateTimeStyles.AdjustToUniversal, out var parsedDt2))
                {
                    supportExpiresAt = parsedDt2;
                }
            }
            else
            {
                var supportExpiresStr = principal.FindFirst("support_expires_at")?.Value;
                if (!string.IsNullOrWhiteSpace(supportExpiresStr) && DateTime.TryParse(supportExpiresStr, null, System.Globalization.DateTimeStyles.AdjustToUniversal, out var parsedDt))
                {
                    supportExpiresAt = parsedDt;
                }
            }

            // Status / Revocation Check
            var status = principal.FindFirst("status")?.Value
                ?? (jwtToken.Payload.TryGetValue("status", out var rawStatus) ? rawStatus?.ToString() : null);
            if (string.Equals(status, "revoked", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("License has been revoked.");
                return LicenseValidationResult.Failure(LicenseValidationFailureReason.Revoked, "لایسنس نرم‌افزار توسط سرور مرجع ابطال شده است (Revoked).");
            }

            var info = new LicenseInfo
            {
                LicenseId = licId,
                CustomerName = customerName,
                CustomerId = customerId,
                Modules = modules,
                MaxUsers = maxUsers,
                ModuleUserLimits = moduleLimits,
                HardwareFingerprint = hardwareFingerprint,
                IssuedAtUtc = issuedAt,
                ExpiresAtUtc = expiresAt,
                LicenseType = licenseType,
                IsPerpetual = isPerpetual,
                OnlineCheckRequired = onlineCheckRequired,
                SupportExpiresAtUtc = supportExpiresAt
            };

            return LicenseValidationResult.Success(info);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed extracting claims from license.");
            return LicenseValidationResult.Failure(LicenseValidationFailureReason.Malformed, $"Error reading claims: {ex.Message}");
        }
    }

    /// <summary>
    /// Asynchronously validates the license content, including hardware fingerprint comparison against the current server.
    /// </summary>
    public Task<LicenseValidationResult> ValidateAsync(string? licenseFileContent, CancellationToken cancellationToken = default)
    {
        // 1. Signature, structure, and expiry validation via Validate()
        var baseResult = Validate(licenseFileContent);
        if (!baseResult.IsValid || baseResult.License == null)
        {
            return Task.FromResult(baseResult);
        }

        // 2. Hardware Fingerprint Validation
        if (_fingerprintService != null)
        {
            try
            {
                var currentFingerprint = _fingerprintService.GetFingerprint();
                var licenseFingerprint = baseResult.License.HardwareFingerprint;

                if (string.IsNullOrWhiteSpace(licenseFingerprint))
                {
                    _logger.LogWarning("License does not contain a hardware fingerprint claim.");
                    return Task.FromResult(LicenseValidationResult.Failure(
                        LicenseValidationFailureReason.FingerprintMismatch,
                        "Hardware fingerprint is missing in the license file."));
                }

                if (!string.Equals(currentFingerprint.Trim(), licenseFingerprint.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("Hardware fingerprint mismatch. Current: {Current}, License: {License}",
                        currentFingerprint, licenseFingerprint);
                    return Task.FromResult(LicenseValidationResult.Failure(
                        LicenseValidationFailureReason.FingerprintMismatch,
                        $"Hardware fingerprint mismatch. Expected server fingerprint '{currentFingerprint}', but license was issued for '{licenseFingerprint}'."));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error computing server hardware fingerprint during license validation.");
                return Task.FromResult(LicenseValidationResult.Failure(
                    LicenseValidationFailureReason.FingerprintMismatch,
                    $"Failed to compute server hardware fingerprint: {ex.Message}"));
            }
        }

        return Task.FromResult(baseResult);
    }

    private static RsaSecurityKey LoadEmbeddedPublicKey()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(EmbeddedKeyResourceName);

        string pemContent;
        if (stream != null)
        {
            using var reader = new StreamReader(stream);
            pemContent = reader.ReadToEnd();
        }
        else
        {
            // Fallback to direct file path if running in tests or pre-compiled development
            var filePath = Path.Combine(AppContext.BaseDirectory, "Services", "Licensing", "license-public.pem");
            if (File.Exists(filePath))
            {
                pemContent = File.ReadAllText(filePath);
            }
            else
            {
                // Also check project directory relative to execution
                var projectRelativePath = Path.Combine(Directory.GetCurrentDirectory(), "Services", "Licensing", "license-public.pem");
                if (File.Exists(projectRelativePath))
                {
                    pemContent = File.ReadAllText(projectRelativePath);
                }
                else
                {
                    throw new InvalidOperationException($"Embedded public key resource '{EmbeddedKeyResourceName}' or file 'license-public.pem' not found. Ensure the public key is embedded in OfficeAutomation.");
                }
            }
        }

        var rsa = RSA.Create();
        rsa.ImportFromPem(pemContent);
        return new RsaSecurityKey(rsa);
    }
}
