using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging.Abstractions;
using OfficeAutomation.Services.Licensing;
using Xunit;

namespace OfficeAutomation.Tests;

public class HardwareFingerprintServiceTests
{
    [Fact]
    public void GetFingerprint_ReturnsNonEmptySha256Hex()
    {
        // Arrange
        var service = new HardwareFingerprintService(NullLogger<HardwareFingerprintService>.Instance);

        // Act
        var fingerprint = service.GetFingerprint();

        // Assert
        Assert.NotNull(fingerprint);
        Assert.NotEmpty(fingerprint);
        // SHA-256 hex string is exactly 64 uppercase hexadecimal characters
        Assert.Equal(64, fingerprint.Length);
        Assert.Matches("^[0-9A-F]{64}$", fingerprint);
    }

    [Fact]
    public void GetActivationCode_ReturnsExpectedGroupedFormat()
    {
        // Arrange
        var service = new HardwareFingerprintService(NullLogger<HardwareFingerprintService>.Instance);

        // Act
        var activationCode = service.GetActivationCode();

        // Assert
        Assert.NotNull(activationCode);
        Assert.NotEmpty(activationCode);
        // Formatted as XXXX-XXXX-XXXX-XXXX (19 characters total, 4 blocks of 4 alphanumeric chars)
        Assert.Equal(19, activationCode.Length);
        Assert.Matches(@"^[2-9A-HJ-NP-Z]{4}-[2-9A-HJ-NP-Z]{4}-[2-9A-HJ-NP-Z]{4}-[2-9A-HJ-NP-Z]{4}$", activationCode);
    }

    [Fact]
    public void GetFingerprint_IsDeterministic_AcrossMultipleInstancesAndInvocations()
    {
        // Simulating multiple service instances (e.g. app restart / distinct singleton lifecycles)
        // Arrange
        var instance1 = new HardwareFingerprintService(NullLogger<HardwareFingerprintService>.Instance);
        var instance2 = new HardwareFingerprintService(NullLogger<HardwareFingerprintService>.Instance);

        // Act
        var fp1 = instance1.GetFingerprint();
        var code1 = instance1.GetActivationCode();

        var fp2 = instance2.GetFingerprint();
        var code2 = instance2.GetActivationCode();

        // Assert
        Assert.Equal(fp1, fp2);
        Assert.Equal(code1, code2);
    }

    [Fact]
    public void GetDetails_ProvidesConstituentHardwareIdentifiers()
    {
        // Arrange
        var service = new HardwareFingerprintService(NullLogger<HardwareFingerprintService>.Instance);

        // Act
        var details = service.GetDetails();

        // Assert
        Assert.NotNull(details);
        Assert.NotEmpty(details.MachineGuid);
        Assert.NotEmpty(details.DiskVolumeSerial);
        Assert.NotEmpty(details.CpuId);
        Assert.NotEmpty(details.FingerprintHash);
        Assert.NotEmpty(details.ActivationCode);
        Assert.Equal(details.FingerprintHash, service.GetFingerprint());
        Assert.Equal(details.ActivationCode, service.GetActivationCode());
    }
}
