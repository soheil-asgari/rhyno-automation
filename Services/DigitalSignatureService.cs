using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;

namespace OfficeAutomation.Services;

public interface IDigitalSignatureService
{
    string CanonicalizePayload<TPayload>(TPayload payload);
    Task<DigitalSignatureResult> SignPayloadAsync<TPayload>(
        TPayload payload,
        X509Certificate2 signingCertificate,
        string? keyId = null,
        CancellationToken cancellationToken = default);
    Task<bool> VerifySignatureAsync<TPayload>(
        TPayload payload,
        string signature,
        X509Certificate2 certificate,
        string hashAlgorithm = "SHA256",
        CancellationToken cancellationToken = default);
}

public sealed class DigitalSignatureService : IDigitalSignatureService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    public string CanonicalizePayload<TPayload>(TPayload payload)
    {
        var json = JsonSerializer.Serialize(payload, SerializerOptions);
        using var document = JsonDocument.Parse(json);
        var builder = new StringBuilder();
        WriteCanonicalElement(document.RootElement, builder);
        return builder.ToString();
    }

    public Task<DigitalSignatureResult> SignPayloadAsync<TPayload>(
        TPayload payload,
        X509Certificate2 signingCertificate,
        string? keyId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(signingCertificate);
        cancellationToken.ThrowIfCancellationRequested();

        if (!signingCertificate.HasPrivateKey)
        {
            throw new InvalidOperationException("The signing certificate must include a private key.");
        }

        using var privateKey = signingCertificate.GetRSAPrivateKey()
            ?? throw new InvalidOperationException("The signing certificate must use an RSA private key.");

        var canonicalPayload = CanonicalizePayload(payload);
        var payloadBytes = Encoding.UTF8.GetBytes(canonicalPayload);
        var payloadHash = Convert.ToHexString(SHA256.HashData(payloadBytes));
        var signatureBytes = privateKey.SignData(payloadBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var signatureKeyId = string.IsNullOrWhiteSpace(keyId)
            ? BuildCertificateKeyId(signingCertificate)
            : keyId.Trim();

        return Task.FromResult(new DigitalSignatureResult(
            canonicalPayload,
            payloadHash,
            "SHA256",
            Convert.ToBase64String(signatureBytes),
            signatureKeyId,
            signingCertificate.Thumbprint,
            signingCertificate.Subject));
    }

    public Task<bool> VerifySignatureAsync<TPayload>(
        TPayload payload,
        string signature,
        X509Certificate2 certificate,
        string hashAlgorithm = "SHA256",
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(certificate);
        cancellationToken.ThrowIfCancellationRequested();

        if (!string.Equals(hashAlgorithm, "SHA256", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException($"Hash algorithm '{hashAlgorithm}' is not supported.");
        }

        using var publicKey = certificate.GetRSAPublicKey()
            ?? throw new InvalidOperationException("The verification certificate must expose an RSA public key.");

        var canonicalPayload = CanonicalizePayload(payload);
        var payloadBytes = Encoding.UTF8.GetBytes(canonicalPayload);
        var signatureBytes = Convert.FromBase64String(signature);
        var isValid = publicKey.VerifyData(payloadBytes, signatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return Task.FromResult(isValid);
    }

    private static string BuildCertificateKeyId(X509Certificate2 certificate)
    {
        return string.IsNullOrWhiteSpace(certificate.Thumbprint)
            ? certificate.GetSerialNumberString()
            : certificate.Thumbprint;
    }

    private static void WriteCanonicalElement(JsonElement element, StringBuilder builder)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                builder.Append('{');
                var firstProperty = true;
                foreach (var property in element.EnumerateObject().OrderBy(item => item.Name, StringComparer.Ordinal))
                {
                    if (!firstProperty)
                    {
                        builder.Append(',');
                    }

                    firstProperty = false;
                    builder.Append(JsonSerializer.Serialize(property.Name));
                    builder.Append(':');
                    WriteCanonicalElement(property.Value, builder);
                }

                builder.Append('}');
                break;

            case JsonValueKind.Array:
                builder.Append('[');
                var firstItem = true;
                foreach (var item in element.EnumerateArray())
                {
                    if (!firstItem)
                    {
                        builder.Append(',');
                    }

                    firstItem = false;
                    WriteCanonicalElement(item, builder);
                }

                builder.Append(']');
                break;

            default:
                builder.Append(element.GetRawText());
                break;
        }
    }
}

public sealed record DigitalSignatureResult(
    string CanonicalPayload,
    string PayloadHash,
    string HashAlgorithm,
    string Signature,
    string SignatureKeyId,
    string? CertificateThumbprint,
    string? CertificateSubject);
