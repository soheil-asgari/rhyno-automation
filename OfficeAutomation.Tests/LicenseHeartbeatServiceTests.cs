using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using OfficeAutomation.Services.Licensing;
using Xunit;

namespace OfficeAutomation.Tests;

public class LicenseHeartbeatServiceTests : IDisposable
{
    private readonly RSA _rsaIssuer;
    private readonly RSA _rsaAppPublic;
    private readonly string _tempDir;
    private readonly Mock<IHostEnvironment> _mockEnv;
    private readonly Mock<IHardwareFingerprintService> _mockFpService;
    private const string ServerFingerprint = "HW-FP-HEARTBEAT-TEST-001";

    public LicenseHeartbeatServiceTests()
    {
        _rsaIssuer = RSA.Create(2048);
        var pubPem = _rsaIssuer.ExportSubjectPublicKeyInfoPem();
        _rsaAppPublic = RSA.Create();
        _rsaAppPublic.ImportFromPem(pubPem);

        _tempDir = Path.Combine(Path.GetTempPath(), "rhyno-hb-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);

        _mockEnv = new Mock<IHostEnvironment>();
        _mockEnv.Setup(e => e.ContentRootPath).Returns(_tempDir);

        _mockFpService = new Mock<IHardwareFingerprintService>();
        _mockFpService.Setup(s => s.GetFingerprint()).Returns(ServerFingerprint);
        _mockFpService.Setup(s => s.GetActivationCode()).Returns("AAAA-BBBB-CCCC-DDDD");
    }

    public void Dispose()
    {
        _rsaIssuer.Dispose();
        _rsaAppPublic.Dispose();
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }
        catch { }
    }

    private string CreateToken(bool onlineCheckRequired, DateTime? issuedAtUtc = null, DateTime? expiresAtUtc = null)
    {
        var generator = new LicenseIssuer.LicenseTokenGenerator();
        var request = new LicenseIssuer.LicenseIssueRequest
        {
            LicenseId = Guid.NewGuid(),
            CustomerName = "Heartbeat Test Client",
            CustomerId = "CLI-HB-01",
            Modules = new List<string> { "Contracts", "Financial" },
            MaxUsers = 50,
            HardwareFingerprint = ServerFingerprint,
            IssuedAtUtc = issuedAtUtc ?? DateTime.UtcNow.AddDays(-1),
            ExpiresAtUtc = expiresAtUtc ?? DateTime.UtcNow.AddDays(30),
            OnlineCheckRequired = onlineCheckRequired,
            LicenseType = "subscription"
        };

        return generator.GenerateLicenseToken(request, _rsaIssuer);
    }

    private (LicenseContext context, Mock<HttpMessageHandler> handlerMock, LicenseHeartbeatService service) CreateSetup(
        string licenseToken,
        LicensingOptions options,
        ILicenseUserEnforcementService? enforcementService = null)
    {
        var validator = new LicenseValidator(_rsaAppPublic, NullLogger<LicenseValidator>.Instance, _mockFpService.Object);
        var optsWrapper = Options.Create(options);
        var licenseContext = new LicenseContext(validator, _mockEnv.Object, NullLogger<LicenseContext>.Instance, optsWrapper);

        // Activate the initial license
        licenseContext.ActivateLicenseAsync(licenseToken).GetAwaiter().GetResult();

        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        var httpClient = new HttpClient(handlerMock.Object);

        var clientFactoryMock = new Mock<IHttpClientFactory>();
        clientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var mockServiceProvider = new Mock<IServiceProvider>();
        var mockServiceScope = new Mock<IServiceScope>();
        var mockScopeServiceProvider = new Mock<IServiceProvider>();
        var mockScopeFactory = new Mock<IServiceScopeFactory>();

        mockScopeFactory.Setup(s => s.CreateScope()).Returns(mockServiceScope.Object);
        mockServiceScope.Setup(s => s.ServiceProvider).Returns(mockScopeServiceProvider.Object);
        mockServiceProvider.Setup(s => s.GetService(typeof(IServiceScopeFactory))).Returns(mockScopeFactory.Object);

        if (enforcementService != null)
        {
            mockScopeServiceProvider.Setup(s => s.GetService(typeof(ILicenseUserEnforcementService))).Returns(enforcementService);
        }

        var service = new LicenseHeartbeatService(
            licenseContext,
            _mockFpService.Object,
            clientFactoryMock.Object,
            optsWrapper,
            mockServiceProvider.Object,
            NullLogger<LicenseHeartbeatService>.Instance);

        return (licenseContext, handlerMock, service);
    }

    [Fact]
    public async Task ScenarioA_OnlineCheckRequiredFalse_ServiceNeverCallsOut()
    {
        // (a) OnlineCheckRequired=false — service never calls out
        var token = CreateToken(onlineCheckRequired: false);
        var options = new LicensingOptions
        {
            HeartbeatUrl = "https://licenses.example.com/api/v1/heartbeat",
            ApiKey = "test-key"
        };

        var (context, handlerMock, service) = CreateSetup(token, options);

        // Act: trigger round
        await service.PerformHeartbeatRoundAsync(CancellationToken.None);

        // Assert: No HTTP call was ever initiated
        handlerMock.Protected().Verify(
            "SendAsync",
            Times.Never(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());

        // State remains unaffected
        Assert.True(context.IsLicensed);
        Assert.False(context.IsLocked);
        Assert.Equal(GracePeriodStatus.NotApplicable, context.GraceStatus);
        Assert.Null(context.GraceDaysRemaining);
        Assert.Null(context.GraceWarningBanner);
    }

    [Fact]
    public async Task ScenarioB_UnconfiguredUrl_AppKeepsWorkingSilentlyNoLock()
    {
        // (b) unconfigured URL — app keeps working silently, no lock
        var token = CreateToken(onlineCheckRequired: true);
        var options = new LicensingOptions
        {
            HeartbeatUrl = "", // empty unconfigured
            ApiKey = "",
            HeartbeatGraceDays = 21
        };

        var (context, handlerMock, service) = CreateSetup(token, options);

        // Act
        await service.PerformHeartbeatRoundAsync(CancellationToken.None);

        // Assert: Never dispatched HTTP request
        handlerMock.Protected().Verify(
            "SendAsync",
            Times.Never(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());

        // System keeps working silently without locking
        Assert.True(context.IsLicensed);
        Assert.False(context.IsLocked);
        Assert.Null(context.LockReason);
        Assert.Equal(GracePeriodStatus.Normal, context.GraceStatus);
        Assert.True(context.GraceDaysRemaining > 7);
        Assert.Null(context.GraceWarningBanner);
    }

    [Fact]
    public async Task ScenarioC_SimulatedUnreachableServer_AppWorksWithEscalatingWarningBanner()
    {
        // (c) simulated unreachable server within grace period — app works with escalating warning banner
        var issuedAt = DateTime.UtcNow.AddDays(-15); // 15 days ago with 21 day grace -> 6 days left (Amber)
        var token = CreateToken(onlineCheckRequired: true, issuedAtUtc: issuedAt);
        var options = new LicensingOptions
        {
            HeartbeatUrl = "https://licenses.example.com/api/v1/heartbeat",
            HeartbeatGraceDays = 21
        };

        var (context, handlerMock, service) = CreateSetup(token, options);

        // Mock HTTP request failing with timeout or connection refused
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        // Act: execute heartbeat attempt
        await service.PerformHeartbeatRoundAsync(CancellationToken.None);

        // Assert: App is STILL licensed and working, but shows amber warning banner (<= 7 days left)
        Assert.True(context.IsLicensed);
        Assert.False(context.IsLocked);
        Assert.Equal(GracePeriodStatus.WarningAmber, context.GraceStatus);
        Assert.NotNull(context.GraceWarningBanner);
        Assert.Contains("هشدار اتصال لایسنس", context.GraceWarningBanner);
        Assert.True(context.GraceDaysRemaining <= 7);

        // Now advance baseline to 20 days ago -> 1 day left (Red warning banner)
        await context.UpdateHeartbeatStateAsync(state =>
        {
            state.LastSuccessfulHeartbeatAt = DateTime.UtcNow.AddDays(-20);
        });

        Assert.True(context.IsLicensed);
        Assert.False(context.IsLocked);
        Assert.Equal(GracePeriodStatus.WarningRed, context.GraceStatus);
        Assert.NotNull(context.GraceWarningBanner);
        Assert.Contains("هشدار فوری", context.GraceWarningBanner);
        Assert.True(context.GraceDaysRemaining <= 2);
    }

    [Fact]
    public async Task ScenarioD_GracePeriodExceeded_AppLocks()
    {
        // (d) grace period exceeded — app locks
        var issuedAt = DateTime.UtcNow.AddDays(-25); // 25 days ago with 21 day grace -> 0 days left
        var token = CreateToken(onlineCheckRequired: true, issuedAtUtc: issuedAt);
        var options = new LicensingOptions
        {
            HeartbeatUrl = "https://licenses.example.com/api/v1/heartbeat",
            HeartbeatGraceDays = 21
        };

        var (context, handlerMock, service) = CreateSetup(token, options);

        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Server unreachable"));

        // Act
        await service.PerformHeartbeatRoundAsync(CancellationToken.None);

        // Assert: Grace period exceeded -> System moves to Locked state
        Assert.Equal(GracePeriodStatus.Expired, context.GraceStatus);
        Assert.False(context.IsLicensed);
        Assert.True(context.IsLocked);
        Assert.NotNull(context.LockReason);
        Assert.Contains("مهلت اتصال", context.LockReason);
        Assert.False(context.IsModuleEnabled("Contracts"));
    }

    [Fact]
    public async Task ScenarioE_SimulatedRevokedResponse_AppLocksImmediately()
    {
        // (e) simulated "revoked" response — app locks immediately
        var token = CreateToken(onlineCheckRequired: true);
        var options = new LicensingOptions
        {
            HeartbeatUrl = "https://licenses.example.com/api/v1/heartbeat"
        };

        var (context, handlerMock, service) = CreateSetup(token, options);

        var revokedResponsePayload = new HeartbeatResponse
        {
            Status = "revoked"
        };

        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(revokedResponsePayload))
            });

        // Act
        await service.PerformHeartbeatRoundAsync(CancellationToken.None);

        // Assert: System is locked immediately
        Assert.True(context.IsLocked);
        Assert.False(context.IsLicensed);
        Assert.Equal(LicenseValidationFailureReason.Revoked, context.ValidationResult.FailureReason);
        Assert.Contains("Revoked", context.LockReason);
        Assert.False(context.IsModuleEnabled("Contracts"));
    }

    [Fact]
    public async Task ScenarioF_SimulatedUpdatedResponse_NewLicenseActivatesWithoutRestart()
    {
        // (f) simulated "updated" response — new license activates without restart
        var token = CreateToken(onlineCheckRequired: true);
        var options = new LicensingOptions
        {
            HeartbeatUrl = "https://licenses.example.com/api/v1/heartbeat"
        };

        var (context, handlerMock, service) = CreateSetup(token, options);

        // Initially enabled modules: Contracts, Financial (MaxUsers: 50)
        Assert.Equal(50, context.CurrentLicense!.MaxUsers);
        Assert.False(context.IsModuleEnabled("Warehouse"));

        // Create updated license granting Warehouse and MaxUsers: 150
        var generator = new LicenseIssuer.LicenseTokenGenerator();
        var updatedReq = new LicenseIssuer.LicenseIssueRequest
        {
            LicenseId = Guid.NewGuid(),
            CustomerName = "Updated Customer Org",
            CustomerId = "CLI-HB-01",
            Modules = new List<string> { "Contracts", "Financial", "Warehouse" },
            MaxUsers = 150,
            HardwareFingerprint = ServerFingerprint,
            IssuedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(90),
            OnlineCheckRequired = true
        };
        var updatedJwt = generator.GenerateLicenseToken(updatedReq, _rsaIssuer);

        var updatedResponsePayload = new HeartbeatResponse
        {
            Status = "updated",
            UpdatedLicenseFile = updatedJwt
        };

        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(updatedResponsePayload))
            });

        // Act
        await service.PerformHeartbeatRoundAsync(CancellationToken.None);

        // Assert: New license activated seamlessly without restart
        Assert.True(context.IsLicensed);
        Assert.False(context.IsLocked);
        Assert.NotNull(context.CurrentLicense);
        Assert.Equal("Updated Customer Org", context.CurrentLicense.CustomerName);
        Assert.Equal(150, context.CurrentLicense.MaxUsers);
        Assert.True(context.IsModuleEnabled("Warehouse"));
        Assert.NotNull(context.LastSuccessfulHeartbeatAt);
        Assert.Equal(GracePeriodStatus.Normal, context.GraceStatus);
    }

    [Fact]
    public async Task ScenarioG_CurrentUserCountsFromEnforcementService_IncludedInHeartbeatPayload()
    {
        var token = CreateToken(onlineCheckRequired: true);
        var options = new LicensingOptions
        {
            HeartbeatUrl = "https://licenses.example.com/api/v1/heartbeat"
        };

        var mockEnforcement = new Mock<ILicenseUserEnforcementService>();
        mockEnforcement.Setup(m => m.GetModuleUserUsagesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ModuleUserUsage>
            {
                new() { ModuleName = "Global", ActiveUserCount = 11, MaxUsersAllowed = 50 },
                new() { ModuleName = "Contracts", ActiveUserCount = 8, MaxUsersAllowed = 20 },
                new() { ModuleName = "Financial", ActiveUserCount = 3, MaxUsersAllowed = 10 }
            });

        var (context, handlerMock, service) = CreateSetup(token, options, mockEnforcement.Object);

        string? capturedRequestBody = null;
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) =>
            {
                capturedRequestBody = req.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
            })
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(new HeartbeatResponse { Status = "active" }))
            });

        // Act
        await service.PerformHeartbeatRoundAsync(CancellationToken.None);

        // Assert
        Assert.NotNull(capturedRequestBody);
        var jsonDoc = JsonDocument.Parse(capturedRequestBody);
        var root = jsonDoc.RootElement;

        Assert.True(root.TryGetProperty("lic_id", out var licIdProp));
        Assert.True(root.TryGetProperty("fingerprint", out var fpProp));
        Assert.True(root.TryGetProperty("current_user_counts", out var countsProp));

        Assert.Equal(8, countsProp.GetProperty("Contracts").GetInt32());
        Assert.Equal(3, countsProp.GetProperty("Financial").GetInt32());
        Assert.False(countsProp.TryGetProperty("Global", out _));
    }
}
