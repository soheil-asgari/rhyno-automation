using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;

namespace LicenseIssuer;

public sealed class LicenseTokenGenerator
{
    public const string Issuer = "RhynoAutomation-LicenseAuthority";
    public const string Audience = "RhynoAutomation-OnPremApp";

    public string GenerateLicenseToken(LicenseIssueRequest request, RSA privateKey)
    {
        var signingKey = new RsaSecurityKey(privateKey)
        {
            KeyId = "rhyno-lic-key-1"
        };
        var signingCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.RsaSha256);

        var claims = new Dictionary<string, object>
        {
            ["lic_id"] = request.LicenseId.ToString(),
            ["customer_name"] = request.CustomerName,
            ["customer_id"] = request.CustomerId,
            ["max_users"] = request.MaxUsers,
            ["hardware_fingerprint"] = request.HardwareFingerprint,
            ["license_type"] = request.LicenseType,
            ["is_perpetual"] = request.IsPerpetual,
            ["modules"] = request.Modules
        };

        // Add per-module limits if provided
        if (request.ModuleUserLimits != null && request.ModuleUserLimits.Count > 0)
        {
            claims["module_user_limits"] = request.ModuleUserLimits;
        }

        var issuedAt = request.IssuedAtUtc == default ? DateTime.UtcNow : request.IssuedAtUtc;
        DateTime? expiresAt = request.IsPerpetual ? null : (request.ExpiresAtUtc ?? issuedAt.AddYears(1));

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Audience = Audience,
            Claims = claims,
            IssuedAt = issuedAt,
            NotBefore = issuedAt.AddMinutes(-5), // Clock skew grace
            Expires = expiresAt,
            SigningCredentials = signingCredentials
        };

        var handler = new JwtSecurityTokenHandler();
        var token = handler.CreateToken(tokenDescriptor);
        return handler.WriteToken(token);
    }
}
