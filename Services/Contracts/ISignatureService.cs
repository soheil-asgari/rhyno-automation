using OfficeAutomation.Models;

namespace OfficeAutomation.Services.Contracts;

/// <summary>
/// Extension point for external e-signature providers (DocuSign, Adobe Sign, etc.)
/// </summary>
public interface IExternalSignatureProvider
{
    string ProviderName { get; }

    Task<ExternalSigningSessionResult> CreateSigningSessionAsync(
        ExternalSigningSessionRequest request,
        CancellationToken ct = default);

    Task<ExternalSigningSessionStatus> GetSessionStatusAsync(
        string sessionId,
        CancellationToken ct = default);

    bool VerifyWebhookSignature(string payload, string signatureHeader);
}

public sealed record ExternalSigningSessionRequest(
    string ContractTitle,
    string? ContractNumber,
    IReadOnlyList<ExternalSignerInfo> Signers,
    string? RedirectUrl,
    int? ExpiryHours);

public sealed record ExternalSignerInfo(
    string FullName,
    string Email,
    string? PhoneNumber,
    string? Organization,
    int SignOrder);

public sealed record ExternalSigningSessionResult(
    string SessionId,
    string SigningUrl,
    DateTime CreatedAt,
    DateTime ExpiresAt);

public sealed record ExternalSigningSessionStatus(
    string SessionId,
    string Status,
    IReadOnlyList<ExternalSignerStatus> Signers);

public sealed record ExternalSignerStatus(
    string Email,
    string Status,
    DateTime? SignedAt);

public sealed record InternalSignatureResult(
    string SignatureImageData,
    DateTime SignedAt,
    string SignedByUserId,
    string? SignedIpAddress,
    string? SignedUserAgent);

/// <summary>
/// Contract-specific signature service that coordinates the e-signature workflow:
/// internal signature capture, verification, and external provider link generation.
/// </summary>
public interface ISignatureService
{
    Task<ContractSignatureSigner> CaptureInternalSignatureAsync(
        int contractId,
        string signatureImageData,
        string? comment,
        string ipAddress,
        string userAgent,
        string currentUserId,
        string? signerParty = null,
        string? signerName = null,
        CancellationToken ct = default);

    Task<bool> VerifySignatureAsync(int signerId, CancellationToken ct = default);

    Task<string> GenerateExternalSignerLinkAsync(
        int contractId,
        int signerId,
        CancellationToken ct = default);

    Task<ContractSignatureRequest> CreateSignatureRequestAsync(
        int contractId,
        string title,
        string? message,
        string deliveryMethod,
        string signingOrder,
        bool autoActivateContract,
        int? amendmentId,
        string currentUserId,
        CancellationToken ct = default);

    Task<List<ContractSignatureRequest>> GetSignatureRequestsAsync(
        int contractId,
        CancellationToken ct = default);
}