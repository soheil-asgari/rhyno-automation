using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace OfficeAutomation.Services.Licensing;

public interface IHardwareFingerprintService
{
    /// <summary>
    /// Computes a stable, unique SHA-256 fingerprint hex string for the current server.
    /// </summary>
    string GetFingerprint();

    /// <summary>
    /// Computes a short, human-readable activation code (e.g. XXXX-XXXX-XXXX-XXXX)
    /// derived from the primary SHA-256 fingerprint.
    /// </summary>
    string GetActivationCode();

    /// <summary>
    /// Returns the individual hardware identifiers used to generate the fingerprint.
    /// Useful for administrative diagnostics and debugging.
    /// </summary>
    HardwareFingerprintDetails GetDetails();
}

/// <summary>
/// Detailed breakdown of raw machine identifiers contributing to the hardware fingerprint.
/// </summary>
public sealed class HardwareFingerprintDetails
{
    public string MachineGuid { get; init; } = string.Empty;
    public string DiskVolumeSerial { get; init; } = string.Empty;
    public string CpuId { get; init; } = string.Empty;
    public string FingerprintHash { get; init; } = string.Empty;
    public string ActivationCode { get; init; } = string.Empty;
}

/// <summary>
/// Generates a stable, unique identifier for the server it runs on, suitable for license locking.
/// Combines Windows MachineGuid, primary disk volume serial number, and CPU identifier.
/// </summary>
public sealed class HardwareFingerprintService : IHardwareFingerprintService
{
    private readonly ILogger<HardwareFingerprintService> _logger;

    // In-memory cache for runtime performance and immutability across the process lifetime.
    // Deterministic and re-computed fresh upon process start without tampering risks.
    private readonly object _lock = new();
    private HardwareFingerprintDetails? _cachedDetails;

    public HardwareFingerprintService(ILogger<HardwareFingerprintService> logger)
    {
        _logger = logger;
    }

    public string GetFingerprint() => GetDetails().FingerprintHash;

    public string GetActivationCode() => GetDetails().ActivationCode;

    public HardwareFingerprintDetails GetDetails()
    {
        if (_cachedDetails != null)
        {
            return _cachedDetails;
        }

        lock (_lock)
        {
            if (_cachedDetails != null)
            {
                return _cachedDetails;
            }

            var machineGuid = GetWindowsMachineGuid();
            var diskSerial = GetSystemDiskVolumeSerial();
            var cpuId = GetCpuIdentifier();

            // Construct normalized raw string payload
            var rawCombined = $"MACHINE_GUID:{machineGuid.Trim()}|DISK_SERIAL:{diskSerial.Trim()}|CPU_ID:{cpuId.Trim()}";

            var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawCombined));
            var fingerprintHash = Convert.ToHexString(hashBytes).ToUpperInvariant();
            var activationCode = FormatActivationCode(hashBytes);

            _cachedDetails = new HardwareFingerprintDetails
            {
                MachineGuid = machineGuid,
                DiskVolumeSerial = diskSerial,
                CpuId = cpuId,
                FingerprintHash = fingerprintHash,
                ActivationCode = activationCode
            };

            _logger.LogInformation("Hardware fingerprint generated successfully. ActivationCode: {ActivationCode}", activationCode);
            return _cachedDetails;
        }
    }

    /// <summary>
    /// Reads MachineGuid from HKLM\SOFTWARE\Microsoft\Cryptography.
    /// Fallbacks gracefully if registry access is restricted or on non-Windows environments.
    /// </summary>
    private string GetWindowsMachineGuid()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return GetNonWindowsMachineId();
        }

        try
        {
            // Open 64-bit registry view first to ensure we read native 64-bit HKLM regardless of process bitness
            using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using var cryptoKey = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
            var guid = cryptoKey?.GetValue("MachineGuid")?.ToString();
            if (!string.IsNullOrWhiteSpace(guid))
            {
                return guid.Trim();
            }

            // Fallback to default registry view
            using var defaultCryptoKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
            var defaultGuid = defaultCryptoKey?.GetValue("MachineGuid")?.ToString();
            if (!string.IsNullOrWhiteSpace(defaultGuid))
            {
                return defaultGuid.Trim();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read MachineGuid from Windows Registry.");
        }

        return "UNKNOWN_MACHINE_GUID";
    }

    /// <summary>
    /// Obtains the volume serial number of the primary system drive (typically C:\ or current system drive).
    /// </summary>
    private string GetSystemDiskVolumeSerial()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                var systemRoot = Environment.GetFolderPath(Environment.SpecialFolder.System);
                var rootDrive = Path.GetPathRoot(systemRoot);
                if (string.IsNullOrWhiteSpace(rootDrive))
                {
                    rootDrive = "C:\\";
                }

                if (!rootDrive.EndsWith("\\"))
                {
                    rootDrive += "\\";
                }

                if (GetVolumeInformation(rootDrive, null, 0, out uint serialNumber, out _, out _, null, 0))
                {
                    return serialNumber.ToString("X8");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to retrieve Windows system volume serial number via GetVolumeInformation.");
            }
        }

        // Cross-platform or fallback: look up ready system / fixed drive
        try
        {
            var drives = DriveInfo.GetDrives();
            var systemDrive = drives.FirstOrDefault(d => d.IsReady && d.DriveType == DriveType.Fixed);
            if (systemDrive != null)
            {
                var label = !string.IsNullOrWhiteSpace(systemDrive.VolumeLabel) ? systemDrive.VolumeLabel : "DEFAULT";
                var format = !string.IsNullOrWhiteSpace(systemDrive.DriveFormat) ? systemDrive.DriveFormat : "NTFS";
                var totalSize = systemDrive.TotalSize;
                return $"{systemDrive.Name}_{label}_{format}_{totalSize}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to inspect DriveInfo for system disk serial.");
        }

        return "UNKNOWN_DISK_SERIAL";
    }

    /// <summary>
    /// Obtains CPU identification information: processor identifier, architecture, and core count.
    /// </summary>
    private string GetCpuIdentifier()
    {
        try
        {
            var cpuArch = RuntimeInformation.ProcessArchitecture.ToString();
            var processorCount = Environment.ProcessorCount.ToString();

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var envCpuId = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER");
                var envCpuArch = Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE");
                var envCpuRev = Environment.GetEnvironmentVariable("PROCESSOR_REVISION");

                if (!string.IsNullOrWhiteSpace(envCpuId))
                {
                    return $"{envCpuId.Trim()}|{envCpuArch}|{envCpuRev}|Cores:{processorCount}";
                }
            }

            return $"ARCH:{cpuArch}|CORES:{processorCount}|PLATFORM:{RuntimeInformation.OSDescription}";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve CPU identifier.");
            return "UNKNOWN_CPU_ID";
        }
    }

    private static string GetNonWindowsMachineId()
    {
        try
        {
            if (File.Exists("/etc/machine-id"))
            {
                return File.ReadAllText("/etc/machine-id").Trim();
            }
            if (File.Exists("/var/lib/dbus/machine-id"))
            {
                return File.ReadAllText("/var/lib/dbus/machine-id").Trim();
            }
        }
        catch
        {
            // Ignore fallback
        }
        return "NON_WINDOWS_MACHINE_ID";
    }

    /// <summary>
    /// Creates a 16-character human-readable activation code grouped in 4 blocks of 4 alphanumeric chars:
    /// e.g. XXXX-XXXX-XXXX-XXXX.
    /// Uses Crockford / Base32-like unambiguous uppercase characters (excluding 0, O, 1, I).
    /// </summary>
    private static string FormatActivationCode(byte[] hashBytes)
    {
        const string alphabet = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ"; // 32 unambiguous chars
        var sb = new StringBuilder(19);

        // Extract 16 characters from the hash bytes
        for (int i = 0; i < 16; i++)
        {
            if (i > 0 && i % 4 == 0)
            {
                sb.Append('-');
            }

            // Combine two adjacent bytes with index shift to get good entropy distribution
            int byteIndex = (i * 2) % hashBytes.Length;
            int b = (hashBytes[byteIndex] ^ hashBytes[(byteIndex + 1) % hashBytes.Length]);
            int charIndex = b % alphabet.Length;
            sb.Append(alphabet[charIndex]);
        }

        return sb.ToString();
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetVolumeInformation(
        string lpRootPathName,
        StringBuilder? lpVolumeNameBuffer,
        int nVolumeNameSize,
        out uint lpVolumeSerialNumber,
        out uint lpMaximumComponentLength,
        out uint lpFileSystemFlags,
        StringBuilder? lpFileSystemNameBuffer,
        int nFileSystemNameSize);
}
