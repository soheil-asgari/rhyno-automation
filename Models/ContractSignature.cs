using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OfficeAutomation.Models;

public static class ContractSignatureStatus
{
    public const string Draft = "Draft";
    public const string Pending = "Pending";
    public const string InProgress = "InProgress";
    public const string Completed = "Completed";
    public const string Declined = "Declined";
    public const string Expired = "Expired";
    public const string Cancelled = "Cancelled";
    public static readonly string[] All = [Draft, Pending, InProgress, Completed, Declined, Expired, Cancelled];
}

public static class SignatureSignerType
{
    public const string Internal = "Internal";
    public const string External = "External";
    public const string Digital = "Digital";
    public static readonly string[] All = [Internal, External, Digital];
}

public static class SignatureSignerResult
{
    public const string Pending = "Pending";
    public const string Signed = "Signed";
    public const string Declined = "Declined";
    public const string Expired = "Expired";
    public static readonly string[] All = [Pending, Signed, Declined, Expired];
}

public static class SignatureDeliveryMethod
{
    public const string InApp = "InApp";
    public const string Email = "Email";
    public const string SMS = "SMS";
    public static readonly string[] All = [InApp, Email, SMS];
}

public class ContractSignatureRequest
{
    public int Id { get; set; }
    [Required] public int ContractId { get; set; }
    public Contract? Contract { get; set; }
    public int? AmendmentId { get; set; }
    [ForeignKey(nameof(AmendmentId))] public ContractAmendment? Amendment { get; set; }
    [Required][StringLength(200)] public string Title { get; set; } = string.Empty;
    [StringLength(2000)] public string? Message { get; set; }
    [Required][StringLength(20)] public string Status { get; set; } = ContractSignatureStatus.Draft;
    [Required][StringLength(20)] public string DeliveryMethod { get; set; } = SignatureDeliveryMethod.InApp;
    [Required][StringLength(20)] public string SigningOrder { get; set; } = "Sequential";
    public int? LinkExpiryHours { get; set; }
    public DateTime? LinkExpiresAt { get; set; }
    [StringLength(128)] public string? AccessToken { get; set; }
    public bool AutoActivateContract { get; set; }
    public int CurrentSignerStep { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public List<ContractSignatureSigner> Signers { get; set; } = new();
    public List<ContractSignatureAuditLog> AuditLogs { get; set; } = new();
}

public class ContractSignatureSigner
{
    public int Id { get; set; }
    [Required] public int SignatureRequestId { get; set; }
    public ContractSignatureRequest? SignatureRequest { get; set; }
    [Required][StringLength(20)] public string SignerType { get; set; } = SignatureSignerType.Internal;
    [StringLength(450)] public string? UserId { get; set; }
    public User? User { get; set; }
    [StringLength(150)] public string? FullName { get; set; }
    [StringLength(200)] public string? Email { get; set; }
    [StringLength(20)] public string? PhoneNumber { get; set; }
    [StringLength(200)] public string? Organization { get; set; }
    [StringLength(100)] public string? JobTitle { get; set; }
    [Range(1, 100)] public int SignOrder { get; set; } = 1;
    [Required][StringLength(20)] public string Result { get; set; } = SignatureSignerResult.Pending;
    [StringLength(128)] public string? SignerToken { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime? ViewedAt { get; set; }
    public DateTime? SignedAt { get; set; }
    [StringLength(45)] public string? SignedIpAddress { get; set; }
    [StringLength(500)] public string? SignedUserAgent { get; set; }
    [StringLength(2000)] public string? Comment { get; set; }
    [StringLength(450)] public string? SentByUserId { get; set; }
    public User? SentByUser { get; set; }
    [StringLength(100)] public string? CertificateThumbprint { get; set; }
    /// <summary>تصویر امضا به صورت Base64 (برای نمایش در UI)</summary>
    [Column(TypeName = "nvarchar(max)")]
    public string? SignatureImageData { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class ContractSignatureAuditLog
{
    public int Id { get; set; }
    [Required] public int SignatureRequestId { get; set; }
    public ContractSignatureRequest? SignatureRequest { get; set; }
    public int? SignerId { get; set; }
    [ForeignKey(nameof(SignerId))] public ContractSignatureSigner? Signer { get; set; }
    [Required][StringLength(30)] public string EventType { get; set; } = string.Empty;
    [StringLength(1000)] public string? Description { get; set; }
    [StringLength(450)] public string? PerformedByUserId { get; set; }
    public User? PerformedByUser { get; set; }
    [StringLength(45)] public string? IpAddress { get; set; }
    [StringLength(2000)] public string? Metadata { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
