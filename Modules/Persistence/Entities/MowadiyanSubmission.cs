using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OfficeAutomation.Modules.Persistence.Entities;

/// <summary>
/// Lifecycle status of an invoice submission to the Iranian Tax Portal (سامانه مودیان).
/// </summary>
public enum MowadiyanSubmissionStatus
{
    Draft = 0,
    PendingSubmission = 1,
    Submitted = 2,
    Success = 3,
    RejectedByTaxOrg = 4,
    RetryLimitExceeded = 5
}

/// <summary>
/// Queue record tracking the submission lifecycle of an invoice to the Mowadiyan
/// (Iranian Tax Portal) API, including retry state and the returned Tax Reference
/// ID (شماره پیگیری مالیاتی).
/// </summary>
public class MowadiyanSubmission
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid InvoiceRecordId { get; set; }
    public InvoiceRecord InvoiceRecord { get; set; } = null!;

    /// <summary>Current lifecycle status.</summary>
    public MowadiyanSubmissionStatus Status { get; set; } = MowadiyanSubmissionStatus.Draft;

    /// <summary>Human-readable status used for display.</summary>
    [StringLength(40)]
    public string StatusText { get; set; } = "Draft";

    /// <summary>Official payload JSON sent to the Tax Portal.</summary>
    public string? PayloadJson { get; set; }

    /// <summary>Signed envelope (PKCS#7) produced by the crypto service.</summary>
    public string? SignedEnvelope { get; set; }

    /// <summary>Tax Reference ID (شماره پیگیری مالیاتی) returned by the Tax Portal.</summary>
    [StringLength(100)]
    public string? TaxReferenceId { get; set; }

    /// <summary>Fiscal ID (شناسه مالیاتی) / UID of the invoice as accepted by the portal.</summary>
    [StringLength(100)]
    public string? FiscalId { get; set; }

    /// <summary>Raw response body from the Tax Portal.</summary>
    public string? ResponseJson { get; set; }

    /// <summary>Latest error message / rejection reason from the portal.</summary>
    [StringLength(2000)]
    public string? ErrorMessage { get; set; }

    /// <summary>Number of retry attempts made so far.</summary>
    public int RetryCount { get; set; }

    /// <summary>Maximum retry attempts before the record is marked RetryLimitExceeded.</summary>
    public int MaxRetryCount { get; set; } = 5;

    /// <summary>Indicates the invoice payload was constructed in sandbox mode.</summary>
    public bool IsSandbox { get; set; }

    /// <summary>UTC timestamp of the next scheduled retry (exponential backoff).</summary>
    public DateTime? NextRetryAtUtc { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
    public DateTime? SubmittedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

/// <summary>
/// Persisted Mowadiyan (سامانه مودیان) connection and tax settings.
/// </summary>
public class MowadiyanSettings
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Client ID (شناسه سرویس گیرنده) issued by the Tax Organization.</summary>
    [StringLength(200)]
    public string ClientId { get; set; } = string.Empty;

    /// <summary>Client Secret / Memory Key (کلید حافظه) for token requests.</summary>
    [StringLength(500)]
    public string? MemorySecret { get; set; }

    /// <summary>Path to the PKCS#12 (.pfx) private key certificate.</summary>
    [StringLength(500)]
    public string? PrivateKeyPath { get; set; }

    /// <summary>Password for the private key certificate.</summary>
    [StringLength(200)]
    public string? PrivateKeyPassword { get; set; }

    /// <summary>Economic code (کد اقتصادی) of the taxpayer.</summary>
    [StringLength(50)]
    public string EconomicCode { get; set; } = string.Empty;

    /// <summary>National ID (شناسه ملی) of the taxpayer.</summary>
    [StringLength(20)]
    public string? NationalId { get; set; }

    /// <summary>
    /// Web-portal login username used to authenticate against the Tax Portal
    /// (سامانه مودیان) for requesting output files (درخواست فایل خروجی).
    /// </summary>
    [StringLength(200)]
    public string PortalUsername { get; set; } = string.Empty;

    /// <summary>Web-portal login password for the Tax Portal output-file session.</summary>
    [StringLength(500)]
    public string? PortalPassword { get; set; }

    /// <summary>Base URL of the Tax Portal API.</summary>
    [StringLength(300)]
    public string BaseUrl { get; set; } = "https://tp.tax.gov.ir";

    /// <summary>When true, the sandbox client is used for offline development.</summary>
    public bool IsSandbox { get; set; } = true;

    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}