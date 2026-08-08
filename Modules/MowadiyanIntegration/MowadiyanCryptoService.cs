using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Microsoft.Extensions.Logging;
using OfficeAutomation.Modules.Persistence.Entities;

namespace OfficeAutomation.Modules.MowadiyanIntegration;

/// <summary>
/// Result of a digital signature operation for Mowadiyan HTTP headers.
/// </summary>
public sealed class MowadiyanSignatureResult
{
    /// <summary>Base64-encoded signature value.</summary>
    public string Signature { get; set; } = string.Empty;

    /// <summary>Signature key ID (شناسه کلید امضا) derived from the certificate.</summary>
    public string SignatureKeyId { get; set; } = string.Empty;

    /// <summary>Signature algorithm identifier (e.g. RSA-SHA256).</summary>
    public string SignatureAlgorithm { get; set; } = "RSA-SHA256";

    /// <summary>Names of the headers that were signed, comma separated.</summary>
    public string SignatureHeaders { get; set; } = string.Empty;
}

/// <summary>
/// PKI / digital signature engine for the Mowadiyan (سامانه مودیان) Tax Portal.
/// Loads an X509 private-key certificate and generates the "Normal" / "Asymmetric"
/// signatures required by the portal HTTP headers (signature, signature-key-id,
/// signature-algorithm, signature-headers, trace-number, timestamp).
/// </summary>
public interface IMowadiyanCryptoService
{
    /// <summary>Loads the X509 certificate for the configured private key path.</summary>
    X509Certificate2 LoadCertificate(MowadiyanSettings settings);

    /// <summary>Creates a raw RSA-SHA256 signature over the given data.</summary>
    string SignData(byte[] data, MowadiyanSettings settings);

    /// <summary>Wraps the payload in a PKCS#7 detached/digest envelope (پاکت PKCS7).</summary>
    string CreatePkcs7Envelope(byte[] data, MowadiyanSettings settings);

    /// <summary>Generates the portal signature headers for a request.</summary>
    MowadiyanSignatureResult CreateSignatureHeaders(
        string httpMethod,
        string pathAndQuery,
        byte[] payload,
        string timestamp,
        string traceNumber,
        string clientId,
        MowadiyanSettings settings);
}
/// <inheritdoc />
public sealed class MowadiyanCryptoService : IMowadiyanCryptoService
{
    private readonly ILogger<MowadiyanCryptoService> _logger;

    public MowadiyanCryptoService(ILogger<MowadiyanCryptoService> logger)
    {
        _logger = logger;
    }

    public X509Certificate2 LoadCertificate(MowadiyanSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.PrivateKeyPath) || !File.Exists(settings.PrivateKeyPath))
        {
            throw new InvalidOperationException(
                "مسیر کلید خصوصی (PrivateKeyPath) معتبر نیست یا فایل گواهی یافت نشد.");
        }

        try
        {
            var cert = new X509Certificate2(
                settings.PrivateKeyPath,
                settings.PrivateKeyPassword ?? string.Empty,
                X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet);

            if (!cert.HasPrivateKey)
            {
                throw new InvalidOperationException("گواهی انتخاب‌شده شامل کلید خصوصی نیست.");
            }

            _logger.LogInformation(
                "Mowadiyan certificate loaded: Subject={Subject}, Thumbprint={Thumbprint}",
                cert.Subject, cert.Thumbprint);

            return cert;
        }
        catch (CryptographicException ex)
        {
            _logger.LogError(ex, "Failed to load Mowadiyan private key certificate");
            throw new InvalidOperationException("خطا در بارگذاری گواهی کلید خصوصی سامانه مودیان.", ex);
        }
    }

    public string SignData(byte[] data, MowadiyanSettings settings)
    {
        using var cert = LoadCertificate(settings);
        using var rsa = cert.GetRSAPrivateKey()
            ?? throw new InvalidOperationException("کلید خصوصی RSA در گواهی یافت نشد.");

        var signature = rsa.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return Convert.ToBase64String(signature);
    }

    public string CreatePkcs7Envelope(byte[] data, MowadiyanSettings settings)
    {
        using var cert = LoadCertificate(settings);

        var content = new ContentInfo(data);
        var signedCms = new SignedCms(content, detached: true);
        var signer = new CmsSigner(cert)
        {
            DigestAlgorithm = Oid.FromFriendlyName("SHA256", OidGroup.HashAlgorithm)
        };
        signedCms.ComputeSignature(signer);

        return Convert.ToBase64String(signedCms.Encode());
    }

    public MowadiyanSignatureResult CreateSignatureHeaders(
        string httpMethod,
        string pathAndQuery,
        byte[] payload,
        string timestamp,
        string traceNumber,
        string clientId,
        MowadiyanSettings settings)
    {
        using var cert = LoadCertificate(settings);

        // The canonical string that the portal verifies — includes the request
        // method, path+query, base64 payload, timestamp, trace number and client id.
        var canonical = string.Join(
            "\n",
            httpMethod.ToUpperInvariant(),
            pathAndQuery,
            Convert.ToBase64String(payload),
            timestamp,
            traceNumber,
            clientId);

        var canonicalBytes = Encoding.UTF8.GetBytes(canonical);
        var signature = SignData(canonicalBytes, settings);

        // Signature key ID: last 16 hex chars of the certificate thumbnail (common practice).
        var thumbprint = cert.Thumbprint ?? string.Empty;
        var keyId = thumbprint.Length >= 16
            ? thumbprint.Substring(thumbprint.Length - 16)
            : thumbprint;

        _logger.LogInformation(
            "Mowadiyan signature headers generated for {Method} {Path}",
            httpMethod, pathAndQuery);

        return new MowadiyanSignatureResult
        {
            Signature = signature,
            SignatureKeyId = keyId,
            SignatureAlgorithm = "RSA-SHA256",
            SignatureHeaders = "method,path,payload,timestamp,trace-number,client-id"
        };
    }
}