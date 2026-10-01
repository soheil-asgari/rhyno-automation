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
        var supportExpires = new DateTime(2027, 12, 31, 23, 59, 59, DateTimeKind.Utc);
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
            IsPerpetual = false,
            OnlineCheckRequired = false,
            SupportExpiresAtUtc = supportExpires
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
        Assert.Equal(System.Text.Json.JsonValueKind.False, root.GetProperty("online_check_required").ValueKind);
        Assert.False(root.GetProperty("online_check_required").GetBoolean());
        Assert.True(root.TryGetProperty("support_expires_at", out var supportExpProp));
        Assert.Equal(System.Text.Json.JsonValueKind.String, supportExpProp.ValueKind);
        Assert.Equal(supportExpires, supportExpProp.GetDateTime().ToUniversalTime());
        Assert.Equal(System.Text.Json.JsonValueKind.Object, root.GetProperty("module_user_limits").ValueKind);
        Assert.Equal(50, root.GetProperty("module_user_limits").GetProperty("Contracts").GetInt32());

        // Deserialization round-trip
        var deserialized = System.Text.Json.JsonSerializer.Deserialize<LicenseInfo>(json);
        Assert.NotNull(deserialized);
        Assert.Equal(id, deserialized.LicenseId);
        Assert.Equal(75, deserialized.MaxUsers);
        Assert.False(deserialized.IsPerpetual);
        Assert.False(deserialized.OnlineCheckRequired);
        Assert.Equal(supportExpires, deserialized.SupportExpiresAtUtc);
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

    [Fact]
    public async Task ValidateAsync_HardwareFingerprintMatches_ReturnsSuccess()
    {
        // Arrange
        var serverFingerprint = "HW-FP-SERVER-MATCH-999";
        var mockFpService = new Moq.Mock<IHardwareFingerprintService>();
        mockFpService.Setup(s => s.GetFingerprint()).Returns(serverFingerprint);

        var generator = new LicenseIssuer.LicenseTokenGenerator();
        var request = new LicenseIssuer.LicenseIssueRequest
        {
            LicenseId = Guid.NewGuid(),
            CustomerName = "Matching Corp",
            CustomerId = "MATCH-01",
            Modules = new List<string> { "Contracts", "Financial" },
            MaxUsers = 10,
            HardwareFingerprint = serverFingerprint,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(30)
        };

        var jwt = generator.GenerateLicenseToken(request, _rsaIssuer);
        var validator = new LicenseValidator(_rsaAppPublic, NullLogger<LicenseValidator>.Instance, mockFpService.Object);

        // Act
        var result = await validator.ValidateAsync(jwt);

        // Assert
        Assert.True(result.IsValid);
        Assert.NotNull(result.License);
        Assert.Equal(serverFingerprint, result.License.HardwareFingerprint);
        Assert.Equal("Matching Corp", result.License.CustomerName);
    }

    [Fact]
    public async Task ValidateAsync_HardwareFingerprintMismatch_ReturnsFingerprintMismatchReason()
    {
        // Arrange
        var serverFingerprint = "HW-FP-SERVER-ACTUAL-123";
        var licenseFingerprint = "HW-FP-LICENSE-DIFFERENT-456";

        var mockFpService = new Moq.Mock<IHardwareFingerprintService>();
        mockFpService.Setup(s => s.GetFingerprint()).Returns(serverFingerprint);

        var generator = new LicenseIssuer.LicenseTokenGenerator();
        var request = new LicenseIssuer.LicenseIssueRequest
        {
            LicenseId = Guid.NewGuid(),
            CustomerName = "Mismatch Corp",
            CustomerId = "MISMATCH-01",
            Modules = new List<string> { "Contracts" },
            MaxUsers = 5,
            HardwareFingerprint = licenseFingerprint,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(30)
        };

        var jwt = generator.GenerateLicenseToken(request, _rsaIssuer);
        var validator = new LicenseValidator(_rsaAppPublic, NullLogger<LicenseValidator>.Instance, mockFpService.Object);

        // Act
        var result = await validator.ValidateAsync(jwt);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(LicenseValidationFailureReason.FingerprintMismatch, result.FailureReason);
        Assert.Contains("fingerprint mismatch", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LicenseContext_MissingFile_GracefullyDegradesToLockedMode()
    {
        // Arrange
        var mockValidator = new Moq.Mock<ILicenseValidator>();
        var mockEnv = new Moq.Mock<Microsoft.Extensions.Hosting.IHostEnvironment>();
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        mockEnv.Setup(e => e.ContentRootPath).Returns(tempDir);

        var context = new LicenseContext(mockValidator.Object, mockEnv.Object, NullLogger<LicenseContext>.Instance);

        // Act
        var refreshResult = await context.RefreshAsync();

        // Assert
        Assert.False(refreshResult.IsValid);
        Assert.True(context.IsLocked);
        Assert.False(context.IsLicensed);
        Assert.Null(context.CurrentLicense);
        Assert.False(context.IsModuleEnabled("Contracts"));
        Assert.NotNull(context.LockReason);
        Assert.Contains("لایسنس", context.LockReason);

        // Cleanup
        try { Directory.Delete(tempDir, true); } catch { }
    }

    [Fact]
    public async Task LicenseContext_ActivateValidLicense_CachesAndEnablesModules()
    {
        // Arrange
        var serverFingerprint = "HW-FP-ACTIVATE-TEST";
        var mockFpService = new Moq.Mock<IHardwareFingerprintService>();
        mockFpService.Setup(s => s.GetFingerprint()).Returns(serverFingerprint);

        var validator = new LicenseValidator(_rsaAppPublic, NullLogger<LicenseValidator>.Instance, mockFpService.Object);
        var mockEnv = new Moq.Mock<Microsoft.Extensions.Hosting.IHostEnvironment>();
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        mockEnv.Setup(e => e.ContentRootPath).Returns(tempDir);

        var context = new LicenseContext(validator, mockEnv.Object, NullLogger<LicenseContext>.Instance);

        var generator = new LicenseIssuer.LicenseTokenGenerator();
        var request = new LicenseIssuer.LicenseIssueRequest
        {
            LicenseId = Guid.NewGuid(),
            CustomerName = "Activated Customer",
            CustomerId = "ACT-01",
            Modules = new List<string> { "Contracts", "Financial" },
            MaxUsers = 50,
            HardwareFingerprint = serverFingerprint,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(60)
        };
        var jwt = generator.GenerateLicenseToken(request, _rsaIssuer);

        // Act
        var activationResult = await context.ActivateLicenseAsync(jwt);

        // Assert
        Assert.True(activationResult.IsValid);
        Assert.True(context.IsLicensed);
        Assert.False(context.IsLocked);
        Assert.NotNull(context.CurrentLicense);
        Assert.Equal("Activated Customer", context.CurrentLicense.CustomerName);
        Assert.True(context.IsModuleEnabled("Contracts"));
        Assert.True(context.IsModuleEnabled("Financial"));
        Assert.False(context.IsModuleEnabled("Warehouse"));

        // Confirm file written to disk
        var savedFile = Path.Combine(tempDir, LicenseContext.DefaultLicenseRelativePath);
        Assert.True(File.Exists(savedFile));
        var diskContent = await File.ReadAllTextAsync(savedFile);
        Assert.Equal(jwt.Trim(), diskContent.Trim());

        // Cleanup
        try { Directory.Delete(tempDir, true); } catch { }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Validate_OnlineCheckRequiredFlag_ValidatedIdentically(bool onlineCheckRequired)
    {
        // Arrange
        var generator = new LicenseIssuer.LicenseTokenGenerator();
        var request = new LicenseIssuer.LicenseIssueRequest
        {
            LicenseId = Guid.NewGuid(),
            CustomerName = "Offline/Online Client",
            CustomerId = "CLI-ONLINECHECK",
            Modules = new List<string> { "Contracts" },
            MaxUsers = 15,
            HardwareFingerprint = "HW-FP-ONLINE-TEST",
            ExpiresAtUtc = DateTime.UtcNow.AddDays(30),
            OnlineCheckRequired = onlineCheckRequired,
            SupportExpiresAtUtc = DateTime.UtcNow.AddYears(1)
        };

        var jwt = generator.GenerateLicenseToken(request, _rsaIssuer);
        var validator = new LicenseValidator(_rsaAppPublic, NullLogger<LicenseValidator>.Instance);

        // Act
        var result = validator.Validate(jwt);

        // Assert: Validated identically purely via signature + expiry + fingerprint (onlineCheckRequired does not affect validity)
        Assert.True(result.IsValid);
        Assert.NotNull(result.License);
        Assert.Equal(onlineCheckRequired, result.License.OnlineCheckRequired);
        Assert.NotNull(result.License.SupportExpiresAtUtc);
        Assert.Equal("Offline/Online Client", result.License.CustomerName);
    }

    [Fact]
    public void Validate_SupportExpiresAt_RoundTripsThroughTokenGenerationAndValidation()
    {
        // Arrange
        var supportDate = new DateTime(2028, 6, 15, 12, 0, 0, DateTimeKind.Utc);
        var generator = new LicenseIssuer.LicenseTokenGenerator();
        var request = new LicenseIssuer.LicenseIssueRequest
        {
            LicenseId = Guid.NewGuid(),
            CustomerName = "Support Contract Client",
            CustomerId = "CLI-SUPPORT-01",
            Modules = new List<string> { "Contracts", "Financial" },
            MaxUsers = 20,
            HardwareFingerprint = "HW-SUPPORT-FP",
            ExpiresAtUtc = DateTime.UtcNow.AddDays(90),
            OnlineCheckRequired = false,
            SupportExpiresAtUtc = supportDate
        };

        var jwt = generator.GenerateLicenseToken(request, _rsaIssuer);

        // Verify claim exists in raw token
        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(jwt);
        Assert.True(token.Payload.TryGetValue("support_expires_at", out var rawSupportExp));
        Assert.NotNull(rawSupportExp);
        Assert.True(token.Payload.TryGetValue("online_check_required", out var rawOnlineCheck));
        Assert.Equal(false, rawOnlineCheck);

        // Validate via validator
        var validator = new LicenseValidator(_rsaAppPublic, NullLogger<LicenseValidator>.Instance);
        var result = validator.Validate(jwt);

        // Assert
        Assert.True(result.IsValid);
        Assert.NotNull(result.License);
        Assert.False(result.License.OnlineCheckRequired);
        Assert.NotNull(result.License.SupportExpiresAtUtc);
        Assert.Equal(supportDate, result.License.SupportExpiresAtUtc.Value);
    }

    [Fact]
    public void Validate_WhenSupportExpiresAtOmitted_LicenseInfoSupportExpiresAtIsNull()
    {
        // Arrange
        var generator = new LicenseIssuer.LicenseTokenGenerator();
        var request = new LicenseIssuer.LicenseIssueRequest
        {
            CustomerName = "No Support Date Client",
            CustomerId = "CLI-NOSUPPORT",
            Modules = new List<string> { "Contracts" },
            HardwareFingerprint = "HW-NOSUPPORT-FP",
            ExpiresAtUtc = DateTime.UtcNow.AddDays(30),
            SupportExpiresAtUtc = null
        };

        var jwt = generator.GenerateLicenseToken(request, _rsaIssuer);
        var validator = new LicenseValidator(_rsaAppPublic, NullLogger<LicenseValidator>.Instance);

        // Act
        var result = validator.Validate(jwt);

        // Assert
        Assert.True(result.IsValid);
        Assert.NotNull(result.License);
        Assert.True(result.License.OnlineCheckRequired); // defaults to true
        Assert.Null(result.License.SupportExpiresAtUtc);
    }

    [Fact]
    public void Validate_AllSevenModules_SuccessfullyVerifiedAndQueried()
    {
        // Arrange
        var generator = new LicenseIssuer.LicenseTokenGenerator();
        var request = new LicenseIssuer.LicenseIssueRequest
        {
            LicenseId = Guid.NewGuid(),
            CustomerName = "Enterprise Full Suite Client",
            CustomerId = "CLI-ALL-MODULES",
            Modules = LicensableModules.All.ToList(),
            MaxUsers = 100,
            HardwareFingerprint = "HW-ALL-MODULES-FP",
            ExpiresAtUtc = DateTime.UtcNow.AddDays(365)
        };

        var jwt = generator.GenerateLicenseToken(request, _rsaIssuer);
        var validator = new LicenseValidator(_rsaAppPublic, NullLogger<LicenseValidator>.Instance);

        // Act
        var result = validator.Validate(jwt);

        // Assert
        Assert.True(result.IsValid);
        Assert.NotNull(result.License);

        foreach (var mod in LicensableModules.All)
        {
            Assert.True(result.License.HasModule(mod), $"Module {mod} should be enabled.");
        }

        // Test alias support for new modules
        Assert.True(result.License.HasModule("Secretariat"));
        Assert.True(result.License.HasModule("office"));
        Assert.True(result.License.HasModule("ai"));
        Assert.True(result.License.HasModule("hr"));
        Assert.False(result.License.HasModule("NonExistentModule"));
    }

    [Fact]
    public void Validate_RevokedStatusToken_ReturnsRevokedFailureReason()
    {
        // Arrange
        var generator = new LicenseIssuer.LicenseTokenGenerator();
        var request = new LicenseIssuer.LicenseIssueRequest
        {
            CustomerName = "Revoked Client",
            CustomerId = "REV-01",
            Modules = new List<string> { "Contracts" },
            MaxUsers = 10,
            HardwareFingerprint = "HW-REVOKED-FP",
            ExpiresAtUtc = DateTime.UtcNow.AddDays(30)
        };

        var jwt = generator.GenerateLicenseToken(request, _rsaIssuer);

        // Inject status: "revoked" into JWT descriptor or simulate token with revoked claim
        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var signingKey = new Microsoft.IdentityModel.Tokens.RsaSecurityKey(_rsaIssuer);
        var signingCredentials = new Microsoft.IdentityModel.Tokens.SigningCredentials(signingKey, Microsoft.IdentityModel.Tokens.SecurityAlgorithms.RsaSha256);
        var descriptor = new Microsoft.IdentityModel.Tokens.SecurityTokenDescriptor
        {
            Issuer = LicenseIssuer.LicenseTokenGenerator.Issuer,
            Audience = LicenseIssuer.LicenseTokenGenerator.Audience,
            Claims = new Dictionary<string, object>
            {
                ["lic_id"] = Guid.NewGuid().ToString(),
                ["customer_name"] = "Revoked Client",
                ["status"] = "revoked",
                ["hardware_fingerprint"] = "HW-REVOKED-FP",
                ["modules"] = new List<string> { "Contracts" },
                ["max_users"] = 10
            },
            Expires = DateTime.UtcNow.AddDays(30),
            SigningCredentials = signingCredentials
        };
        var revokedJwt = handler.CreateEncodedJwt(descriptor);

        var validator = new LicenseValidator(_rsaAppPublic, NullLogger<LicenseValidator>.Instance);

        // Act
        var result = validator.Validate(revokedJwt);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(LicenseValidationFailureReason.Revoked, result.FailureReason);
        Assert.Contains("ابطال", result.ErrorMessage);
    }

    [Fact]
    public async Task LicenseContext_LockedApp_UploadValidLicenseForAllSevenModules_UnlocksImmediatelyWithoutRestart()
    {
        // Arrange
        var serverFingerprint = "SERVER-HW-FP-ENTERPRISE";
        var mockFpService = new Moq.Mock<IHardwareFingerprintService>();
        mockFpService.Setup(s => s.GetFingerprint()).Returns(serverFingerprint);
        mockFpService.Setup(s => s.GetActivationCode()).Returns("AAAA-BBBB-CCCC-DDDD");

        var validator = new LicenseValidator(_rsaAppPublic, NullLogger<LicenseValidator>.Instance, mockFpService.Object);
        var mockEnv = new Moq.Mock<Microsoft.Extensions.Hosting.IHostEnvironment>();
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        mockEnv.Setup(e => e.ContentRootPath).Returns(tempDir);

        var context = new LicenseContext(validator, mockEnv.Object, NullLogger<LicenseContext>.Instance);

        // Initially locked because no license has been loaded
        Assert.True(context.IsLocked);
        Assert.False(context.IsLicensed);
        foreach (var mod in LicensableModules.All)
        {
            Assert.False(context.IsModuleEnabled(mod), $"Module {mod} must be disabled when locked.");
        }

        // Generate full 7 modules license matching server's hardware fingerprint
        var generator = new LicenseIssuer.LicenseTokenGenerator();
        var request = new LicenseIssuer.LicenseIssueRequest
        {
            LicenseId = Guid.NewGuid(),
            CustomerName = "Petro Pars Industrial Group",
            CustomerId = "CLI-PETRO-01",
            Modules = LicensableModules.All.ToList(),
            MaxUsers = 250,
            HardwareFingerprint = serverFingerprint,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(365),
            LicenseType = "subscription"
        };
        var validLicenseFileContent = generator.GenerateLicenseToken(request, _rsaIssuer);

        // Act: Simulate what SettingsController.ActivateLicense does
        var activationResult = await context.ActivateLicenseAsync(validLicenseFileContent);

        // Assert: System is unlocked immediately without restart
        Assert.True(activationResult.IsValid);
        Assert.True(context.IsLicensed);
        Assert.False(context.IsLocked);
        Assert.Null(context.LockReason);
        Assert.NotNull(context.CurrentLicense);
        Assert.Equal("Petro Pars Industrial Group", context.CurrentLicense.CustomerName);
        Assert.Equal(250, context.CurrentLicense.MaxUsers);

        // All 7 modules are enabled instantly
        foreach (var mod in LicensableModules.All)
        {
            Assert.True(context.IsModuleEnabled(mod), $"Module {mod} must be enabled immediately after activation.");
        }

        // Clean up
        try { Directory.Delete(tempDir, true); } catch { }
    }
}

