using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Models;
using OfficeAutomation.Modules.Office.Infrastructure.Persistence;
using System.Text.Json;

namespace OfficeAutomation.Services.Contracts;

/// <summary>
/// Implements the e-signature workflow orchestration: internal signature capture,
/// verification, and external provider link generation.
/// </summary>
public sealed class SignatureService : ISignatureService
{
    private readonly OfficeDbContext _context;
    private readonly IDigitalSignatureService _digitalSignature;
    private readonly ILogger<SignatureService> _logger;

    public SignatureService(
        OfficeDbContext context,
        IDigitalSignatureService digitalSignature,
        ILogger<SignatureService> logger)
    {
        _context = context;
        _digitalSignature = digitalSignature;
        _logger = logger;
    }

    // ── Internal signature capture ─────────────────────────────────────

    public async Task<ContractSignatureSigner> CaptureInternalSignatureAsync(
        int contractId,
        string signatureImageData,
        string? comment,
        string ipAddress,
        string userAgent,
        string currentUserId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(signatureImageData))
            throw new ArgumentException("Signature image data cannot be empty.", nameof(signatureImageData));

        var contract = await _context.Contracts
            .FirstOrDefaultAsync(c => c.Id == contractId, ct)
            ?? throw new InvalidOperationException($"قرارداد با شناسه {contractId} یافت نشد.");

        // Find or create the active signature request for this contract.
        var request = await _context.ContractSignatureRequests
            .Where(r => r.ContractId == contractId && r.Status == ContractSignatureStatus.InProgress)
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync(ct)
            ?? await _context.ContractSignatureRequests
                .Where(r => r.ContractId == contractId && (r.Status == ContractSignatureStatus.Pending || r.Status == ContractSignatureStatus.Draft))
                .OrderByDescending(r => r.CreatedAt)
                .FirstOrDefaultAsync(ct);

        if (request is null)
        {
            request = new ContractSignatureRequest
            {
                ContractId = contractId,
                Title = $"امضای قرارداد {contract.ContractNumber}",
                Status = ContractSignatureStatus.InProgress,
                DeliveryMethod = SignatureDeliveryMethod.InApp,
                SigningOrder = "Sequential",
                AccessToken = Guid.NewGuid().ToString("N"),
                AutoActivateContract = false,
                CurrentSignerStep = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.ContractSignatureRequests.Add(request);
        }

        var signer = await _context.ContractSignatureSigners
            .FirstOrDefaultAsync(s => s.SignatureRequestId == request.Id && s.UserId == currentUserId, ct);

        if (signer is null)
        {
            signer = new ContractSignatureSigner
            {
                SignatureRequestId = request.Id,
                SignerType = SignatureSignerType.Internal,
                UserId = currentUserId,
                SignOrder = request.CurrentSignerStep > 0 ? request.CurrentSignerStep : 1,
                Result = SignatureSignerResult.Pending,
                SignerToken = Guid.NewGuid().ToString("N"),
                CreatedAt = DateTime.UtcNow
            };
            _context.ContractSignatureSigners.Add(signer);
        }

        signer.Result = SignatureSignerResult.Signed;
        signer.SignedAt = DateTime.UtcNow;
        signer.SignedIpAddress = ipAddress;
        signer.SignedUserAgent = userAgent;
        signer.Comment = comment ?? signer.Comment;
        signer.SignatureImageData = signatureImageData;

        _context.ContractSignatureAuditLogs.Add(new ContractSignatureAuditLog
        {
            SignatureRequestId = request.Id,
            SignerId = signer.Id,
            EventType = "Signed",
            Description = "امضای داخلی توسط کاربر ثبت شد",
            PerformedByUserId = currentUserId,
            IpAddress = ipAddress,
            Metadata = JsonSerializer.Serialize(new { signatureLength = signatureImageData.Length }),
            CreatedAt = DateTime.UtcNow
        });

        var pendingCount = await _context.ContractSignatureSigners
            .CountAsync(s => s.SignatureRequestId == request.Id && s.Result != SignatureSignerResult.Signed, ct);
        if (pendingCount == 0)
        {
            request.Status = ContractSignatureStatus.Completed;
            request.CompletedAt = DateTime.UtcNow;
            if (request.AutoActivateContract && contract.Status != ContractStatuses.Active)
            {
                contract.Status = ContractStatuses.Active;
                contract.SignDate ??= DateTime.Today;
            }
        }
        else if (request.Status != ContractSignatureStatus.InProgress)
        {
            request.Status = ContractSignatureStatus.InProgress;
        }
        request.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Internal signature captured for contract {ContractId} by {UserId}", contractId, currentUserId);
        return signer;
    }
// ── Verification ──────────────────────────────────────────────────

    public async Task<bool> VerifySignatureAsync(int signerId, CancellationToken ct = default)
    {
        var signer = await _context.ContractSignatureSigners
            .Include(s => s.SignatureRequest)
            .FirstOrDefaultAsync(s => s.Id == signerId, ct);
        if (signer is null || signer.Result != SignatureSignerResult.Signed || signer.SignedAt is null)
            return false;

        if (signer.SignatureRequest is not null && signer.SignatureRequest.Status == ContractSignatureStatus.Expired)
            return false;

        return true;
    }

    // ── External signer link generation ───────────────────────────────

    public async Task<string> GenerateExternalSignerLinkAsync(
        int contractId,
        int signerId,
        CancellationToken ct = default)
    {
        var signer = await _context.ContractSignatureSigners
            .Include(s => s.SignatureRequest)
            .FirstOrDefaultAsync(s => s.Id == signerId && s.SignatureRequest!.ContractId == contractId, ct)
            ?? throw new InvalidOperationException("امضاکننده یافت نشد.");

        var request = signer.SignatureRequest!;

        signer.SignerToken ??= Guid.NewGuid().ToString("N");
        request.LinkExpiresAt ??= DateTime.UtcNow.AddHours(request.LinkExpiryHours ?? 72);
        signer.SentAt = DateTime.UtcNow;

        _context.ContractSignatureAuditLogs.Add(new ContractSignatureAuditLog
        {
            SignatureRequestId = request.Id,
            SignerId = signer.Id,
            EventType = "LinkGenerated",
            Description = "لینک امضای خارجی تولید شد",
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync(ct);
        return $"/Contracts/SignExternal/{contractId}/{signer.SignerToken}";
    }

    // ── Create signature request ──────────────────────────────────────

    public async Task<ContractSignatureRequest> CreateSignatureRequestAsync(
        int contractId,
        string title,
        string? message,
        string deliveryMethod,
        string signingOrder,
        bool autoActivateContract,
        int? amendmentId,
        string currentUserId,
        CancellationToken ct = default)
    {
        var contract = await _context.Contracts
            .FirstOrDefaultAsync(c => c.Id == contractId, ct)
            ?? throw new InvalidOperationException($"قرارداد با شناسه {contractId} یافت نشد.");

        var request = new ContractSignatureRequest
        {
            ContractId = contractId,
            AmendmentId = amendmentId,
            Title = title,
            Message = message,
            Status = ContractSignatureStatus.Draft,
            DeliveryMethod = deliveryMethod,
            SigningOrder = signingOrder,
            AccessToken = Guid.NewGuid().ToString("N"),
            AutoActivateContract = autoActivateContract,
            LinkExpiryHours = 72,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.ContractSignatureRequests.Add(request);
        await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Signature request created for contract {ContractId}", contractId);
        return request;
    }

    // ── Get signature requests ────────────────────────────────────────

    public async Task<List<ContractSignatureRequest>> GetSignatureRequestsAsync(
        int contractId,
        CancellationToken ct = default)
    {
        return await _context.ContractSignatureRequests
            .AsNoTracking()
            .Include(r => r.Signers).ThenInclude(s => s.User)
            .Include(r => r.AuditLogs)
            .Where(r => r.ContractId == contractId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);
    }
}