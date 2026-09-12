using System.Security.Cryptography;
using Microsoft.Extensions.Logging.Abstractions;
using OfficeAutomation.Services.Licensing;
using Xunit;

namespace OfficeAutomation.Tests;

public class LicenseValidatorTests
{
    private readonly RSA _rsaIssuer;
    private readonly RSA _rsaAppPublic;

    public LicenseValidatorTests()
    {
        _rsaIssuer = RSA.Create(2048);
        var pubPem = _rsaIssuer.ExportSubjectPublicKeyInfoPem();
        _rsaAppPublic = RSA.Create();
        _rsaAppPublic.ImportFromPem(pubPem);
    }

    [Fact]
    public void Validate_ValidToken_ReturnsSuccessWithPopulatedLicenseInfo()
    {
        // Arrange
        var generator = new LicenseIssuer.LicenseTokenGenerator();
        var request = new LicenseIssuer.LicenseIssueRequest
        {
            LicenseId = Guid.NewGuid(),
            CustomerName = "Test Client Co",
            CustomerId = "CLI-99",
            Modules = new List<string> { "Contracts", "Financial" },
            MaxUsers = 40,
            ModuleUserLimits = new Dictionary<string, int> { ["Contracts"] = 25, ["Financial"] = 15 },
            HardwareFingerprint = "HW-FP-TEST-001",
            IssuedAtUtc = DateTime.UtcNow.AddMinutes(-5),
            ExpiresAtUtc = DateTime.UtcNow.AddDays(30),
            LicenseType = "subscription",
            IsPerpetual = false
        };

        var jwt = generator.GenerateLicenseToken(request, _rsaIssuer);
        var validator = new LicenseValidator(_rsaAppPublic, NullLogger<LicenseValidator>.Instance);

        // Act
        var result = validator.Validate(jwt);

        // Assert
        Assert.True(result.IsValid);
        Assert.NotNull(result.License);
        Assert.Equal(request.LicenseId, result.License.LicenseId);
        Assert.Equal("Test Client Co", result.License.CustomerName);
        Assert.Equal("CLI-99", result.License.CustomerId);
        Assert.Equal(40, result.License.MaxUsers);
        Assert.Equal("HW-FP-TEST-001", result.License.HardwareFingerprint);
        Assert.Equal("subscription", result.License.LicenseType);
        Assert.False(result.License.IsPerpetual);
        Assert.True(result.License.HasModule("Contracts"));
        Assert.True(result.License.HasModule("Financial"));
        Assert.False(result.License.HasModule("Warehouse"));
        Assert.Equal(25, result.License.GetMaxUsersForModule("Contracts"));
        Assert.Equal(15, result.License.GetMaxUsersForModule("Financial"));
        Assert.Equal(40, result.License.GetMaxUsersForModule("UnknownModule")); // fallback to global
        Assert.NotNull(result.License.DaysRemaining);
        Assert.True(result.License.DaysRemaining > 0);
    }

    [Fact]
    public void Validate_PayloadClaimTypes_AreNotDoubleEncodedAsStrings()
    {
        // Arrange
        var generator = new LicenseIssuer.LicenseTokenGenerator();
        var request = new LicenseIssuer.LicenseIssueRequest
        {
            LicenseId = Guid.NewGuid(),
            CustomerName = "Type Check Co",
            CustomerId = "CLI-TYPES",
            Modules = new List<string> { "Contracts", "Financial" },
            MaxUsers = 50,
            ModuleUserLimits = new Dictionary<string, int> { ["Contracts"] = 30, ["Financial"] = 20 },
            HardwareFingerprint = "HW-FP-TYPES-001",
            IssuedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(30),
            LicenseType = "subscription",
            IsPerpetual = false
        };

        var jwt = generator.GenerateLicenseToken(request, _rsaIssuer);

        // Read raw token payload JSON
        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(jwt);
        var payloadJson = token.Payload.SerializeToJson();

        using var doc = System.Text.Json.JsonDocument.Parse(payloadJson);
        var root = doc.RootElement;

        // 1. max_users must be a JSON number, not a string ("50" -> 50)
        Assert.True(root.TryGetProperty("max_users", out var maxUsersProp));
        Assert.Equal(System.Text.Json.JsonValueKind.Number, maxUsersProp.ValueKind);
        Assert.Equal(50, maxUsersProp.GetInt32());

        // 2. is_perpetual must be a JSON boolean, not a string ("false" -> false)
        Assert.True(root.TryGetProperty("is_perpetual", out var isPerpetualProp));
        Assert.Equal(System.Text.Json.JsonValueKind.False, isPerpetualProp.ValueKind);
        Assert.False(isPerpetualProp.GetBoolean());

        // 3. module_user_limits must be a nested JSON object claim, not a JSON-encoded string
        Assert.True(root.TryGetProperty("module_user_limits", out var limitsProp));
        Assert.Equal(System.Text.Json.JsonValueKind.Object, limitsProp.ValueKind);
        Assert.Equal(30, limitsProp.GetProperty("Contracts").GetInt32());
        Assert.Equal(20, limitsProp.GetProperty("Financial").GetInt32());

        // Also verify validation succeeds and maps to LicenseInfo correctly
        var validator = new LicenseValidator(_rsaAppPublic, NullLogger<LicenseValidator>.Instance);
        var result = validator.Validate(jwt);
        Assert.True(result.IsValid);
        Assert.NotNull(result.License);
        Assert.Equal(50, result.License.MaxUsers);
        Assert.False(result.License.IsPerpetual);
        Assert.Equal(30, result.License.GetMaxUsersForModule("Contracts"));
        Assert.Equal(20, result.License.GetMaxUsersForModule("Financial"));
    }

    [Fact]
    public void Validate_PerpetualClaimType_IsJsonBooleanTrue()
    {
        var generator = new LicenseIssuer.LicenseTokenGenerator();
        var request = new LicenseIssuer.LicenseIssueRequest
        {
            CustomerName = "Perpetual Co",
            CustomerId = "PERP-02",
            Modules = new List<string> { "Contracts" },
            MaxUsers = 10,
            HardwareFingerprint = "HW-PERP-BOOLEAN",
            IsPerpetual = true,
            LicenseType = "perpetual"
        };

        var jwt = generator.GenerateLicenseToken(request, _rsaIssuer);
        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(jwt);
        var payloadJson = token.Payload.SerializeToJson();

        using var doc = System.Text.Json.JsonDocument.Parse(payloadJson);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("is_perpetual", out var isPerpetualProp));
        Assert.Equal(System.Text.Json.JsonValueKind.True, isPerpetualProp.ValueKind);
        Assert.True(isPerpetualProp.GetBoolean());
    }

    [Fact]
    public void LicenseInfo_JsonSerialization_UsesExpectedPropertyNamesAndTypes()
    {
        var id = Guid.NewGuid();
        var info = new LicenseInfo
        {
            LicenseId = id,
            CustomerName = "Serialization Co",
            CustomerId = "SER-1",
            Modules = new[] { "Contracts" },
            MaxUsers = 75,
            ModuleUserLimits = new Dictionary<string, int> { ["Contracts"] = 50 },
            HardwareFingerprint = "HW-SER-01",
            LicenseType = "subscription",
            IsPerpetual = false
        };

        var json = System.Text.Json.JsonSerializer.Serialize(info);
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal(id.ToString(), root.GetProperty("lic_id").GetString());
        Assert.Equal("Serialization Co", root.GetProperty("customer_name").GetString());
        Assert.Equal("SER-1", root.GetProperty("customer_id").GetString());
        Assert.Equal(System.Text.Json.JsonValueKind.Number, root.GetProperty("max_users").ValueKind);
        Assert.Equal(75, root.GetProperty("max_users").GetInt32());
        Assert.Equal(System.Text.Json.JsonValueKind.False, root.GetProperty("is_perpetual").ValueKind);
        Assert.Equal(System.Text.Json.JsonValueKind.Object, root.GetProperty("module_user_limits").ValueKind);
        Assert.Equal(50, root.GetProperty("module_user_limits").GetProperty("Contracts").GetInt32());

        // Deserialization round-trip
        var deserialized = System.Text.Json.JsonSerializer.Deserialize<LicenseInfo>(json);
        Assert.NotNull(deserialized);
        Assert.Equal(id, deserialized.LicenseId);
        Assert.Equal(75, deserialized.MaxUsers);
        Assert.False(deserialized.IsPerpetual);
        Assert.Equal(50, deserialized.GetMaxUsersForModule("Contracts"));
    }



    [Fact]
    public void Validate_PerpetualLicense_HasNoExpiryAndDaysRemainingIsNull()
    {
        // Arrange
        var generator = new LicenseIssuer.LicenseTokenGenerator();
        var request = new LicenseIssuer.LicenseIssueRequest
        {
            CustomerName = "Perpetual Client",
            CustomerId = "PERP-01",
            Modules = new List<string> { "Contracts" },
            MaxUsers = 10,
            HardwareFingerprint = "HW-PERP-123",
            IsPerpetual = true,
            LicenseType = "perpetual"
        };

        var jwt = generator.GenerateLicenseToken(request, _rsaIssuer);
        var validator = new LicenseValidator(_rsaAppPublic, NullLogger<LicenseValidator>.Instance);

        // Act
        var result = validator.Validate(jwt);

        // Assert
        Assert.True(result.IsValid);
        Assert.NotNull(result.License);
        Assert.True(result.License.IsPerpetual);
        Assert.Null(result.License.ExpiresAtUtc);
        Assert.Null(result.License.DaysRemaining);
    }

    [Fact]
    public void Validate_ExpiredToken_ReturnsExpiredFailureReason()
    {
        // Arrange
        var generator = new LicenseIssuer.LicenseTokenGenerator();
        var request = new LicenseIssuer.LicenseIssueRequest
        {
            CustomerName = "Old Client",
            CustomerId = "OLD-01",
            Modules = new List<string> { "Contracts" },
            MaxUsers = 5,
            HardwareFingerprint = "HW-OLD-001",
            IssuedAtUtc = DateTime.UtcNow.AddDays(-10),
            ExpiresAtUtc = DateTime.UtcNow.AddDays(-1), // Expired yesterday
            IsPerpetual = false
        };

        var jwt = generator.GenerateLicenseToken(request, _rsaIssuer);
        var validator = new LicenseValidator(_rsaAppPublic, NullLogger<LicenseValidator>.Instance);

        // Act
        var result = validator.Validate(jwt);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(LicenseValidationFailureReason.Expired, result.FailureReason);
        Assert.Contains("expired", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_TamperedSignature_ReturnsInvalidSignatureFailureReason()
    {
        // Arrange: sign with a different key
        using var untrustedKey = RSA.Create(2048);
        var generator = new LicenseIssuer.LicenseTokenGenerator();
        var request = new LicenseIssuer.LicenseIssueRequest
        {
            CustomerName = "Hacker",
            CustomerId = "BAD-01",
            Modules = new List<string> { "Contracts" },
            MaxUsers = 9999,
            HardwareFingerprint = "HW-FORGED",
            IsPerpetual = true
        };

        var forgedJwt = generator.GenerateLicenseToken(request, untrustedKey);
        var validator = new LicenseValidator(_rsaAppPublic, NullLogger<LicenseValidator>.Instance);

        // Act
        var result = validator.Validate(forgedJwt);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(LicenseValidationFailureReason.InvalidSignature, result.FailureReason);
    }

    [Fact]
    public void Validate_MalformedToken_ReturnsMalformedFailureReason()
    {
        // Arrange
        var validator = new LicenseValidator(_rsaAppPublic, NullLogger<LicenseValidator>.Instance);

        // Act
        var result = validator.Validate("not.a.valid.jwt.payload");

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(LicenseValidationFailureReason.Malformed, result.FailureReason);
    }

    [Fact]
    public void Validate_EmptyOrNullToken_ReturnsMissingFailureReason()
    {
        // Arrange
        var validator = new LicenseValidator(_rsaAppPublic, NullLogger<LicenseValidator>.Instance);

        // Act
        var resultNull = validator.Validate(null);
        var resultEmpty = validator.Validate("   ");

        // Assert
        Assert.False(resultNull.IsValid);
        Assert.Equal(LicenseValidationFailureReason.Missing, resultNull.FailureReason);

        Assert.False(resultEmpty.IsValid);
        Assert.Equal(LicenseValidationFailureReason.Missing, resultEmpty.FailureReason);
    }
}
