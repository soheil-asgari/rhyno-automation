using System.ComponentModel.DataAnnotations;

namespace OfficeAutomation.Models;

/// <summary>
/// A configurable alert rule that defines when a notification should be raised,
/// the severity level, and which user(s) receive it.
/// </summary>
public class AlertRule
{
    public int Id { get; set; }

    [MaxLength(64)]
    public string? TenantId { get; set; }

    /// <summary>
    /// Which module the rule applies to, e.g. "Contracts", "Workflow".
    /// </summary>
    [Required]
    [MaxLength(60)]
    public string SourceModule { get; set; } = string.Empty;

    /// <summary>
    /// The trigger type, e.g. "ContractExpiring", "ContractOverduePayment",
    /// "ContractStatusChanged", "ApprovalStepAssigned".
    /// </summary>
    [Required]
    [MaxLength(80)]
    public string TriggerType { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable name for this rule (shown in settings UI).
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Optional description of what this rule does.
    /// </summary>
    [MaxLength(1000)]
    public string? Description { get; set; }

    /// <summary>
    /// Default severity level for notifications raised by this rule.
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string Severity { get; set; } = NotificationSeverity.Info;

    /// <summary>
    /// How many days/hours in advance to trigger (for time-based rules like expiry).
    /// Null means trigger on the day of the event.
    /// </summary>
    public int? AdvanceNoticeDays { get; set; }

    /// <summary>
    /// Whether in-app notification is enabled.
    /// </summary>
    public bool SendInApp { get; set; } = true;

    /// <summary>
    /// Whether email notification is enabled (requires user to have an email).
    /// </summary>
    public bool SendEmail { get; set; }

    /// <summary>
    /// Whether this rule is active.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Whether this is a system-defined rule (cannot be deleted by user).
    /// </summary>
    public bool IsSystemRule { get; set; }

    /// <summary>
    /// Optional JSON filter conditions for advanced rule configuration.
    /// </summary>
    [MaxLength(2000)]
    public string? FilterConditions { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public string? CreatedByUserId { get; set; }

    public User? CreatedByUser { get; set; }
}

/// <summary>
/// Represents a user's subscription status for a specific alert rule.
/// Users can opt in/out of each rule for in-app and/or email delivery.
/// </summary>
public class AlertSubscription
{
    public int Id { get; set; }

    [MaxLength(64)]
    public string? TenantId { get; set; }

    [Required]
    [MaxLength(450)]
    public string UserId { get; set; } = string.Empty;

    public int AlertRuleId { get; set; }

    public AlertRule? AlertRule { get; set; }

    /// <summary>
    /// Whether this user receives in-app notifications for this rule.
    /// </summary>
    public bool ReceiveInApp { get; set; } = true;

    /// <summary>
    /// Whether this user receives email notifications for this rule.
    /// </summary>
    public bool ReceiveEmail { get; set; }

    /// <summary>
    /// When the subscription was last modified.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public User? User { get; set; }
}

/// <summary>
/// Static catalog of known trigger types used across the application.
/// </summary>
public static class AlertTriggerTypes
{
    // ── Expiry / Renewal ──────────────────────────────────────────
    public const string ContractExpiring90 = "ContractExpiring90";
    public const string ContractExpiring60 = "ContractExpiring60";
    public const string ContractExpiring30 = "ContractExpiring30";
    public const string ContractExpiring14 = "ContractExpiring14";
    public const string ContractExpiring7 = "ContractExpiring7";
    public const string ContractExpiring1 = "ContractExpiring1";
    public const string ContractExpired = "ContractExpired";
    public const string ContractRenewalDecision = "ContractRenewalDecision";

    // ── Payments ──────────────────────────────────────────────────
    public const string ContractOverduePayment = "ContractOverduePayment";
    public const string ContractPaymentReceived = "ContractPaymentReceived";

    // ── Approval ──────────────────────────────────────────────────
    public const string ContractApprovalRequested = "ContractApprovalRequested";
    public const string ContractApprovalCompleted = "ContractApprovalCompleted";
    public const string ContractApprovalDelegated = "ContractApprovalDelegated";

    // ── Versioning ────────────────────────────────────────────────
    public const string ContractVersionCreated = "ContractVersionCreated";

    // ── Status Changes ────────────────────────────────────────────
    public const string ContractStatusChanged = "ContractStatusChanged";

    // ── Guarantee / Retention (stub — ContractGuarantee entity not yet implemented, see Item 22) ──
    public const string ContractGuaranteeExpiry = "ContractGuaranteeExpiry";

    public static IReadOnlyList<(string TriggerType, string DisplayName, string DefaultSeverity, string Description)> Defaults { get; } =
    [
        // Multi-tier expiry warnings (Item 7 spec: 90/60/30/14/7/1 days)
        (ContractExpiring90,   "انقضای نزدیک — ۹۰ روز مانده",   NotificationSeverity.Info,    "۹۰ روز تا انقضای قرارداد باقی است"),
        (ContractExpiring60,   "انقضای نزدیک — ۶۰ روز مانده",   NotificationSeverity.Info,    "۶۰ روز تا انقضای قرارداد باقی است"),
        (ContractExpiring30,   "انقضای نزدیک — ۳۰ روز مانده",   NotificationSeverity.Warning,  "۳۰ روز تا انقضای قرارداد باقی است"),
        (ContractExpiring14,   "انقضای نزدیک — ۱۴ روز مانده",   NotificationSeverity.Warning,  "۱۴ روز تا انقضای قرارداد باقی است"),
        (ContractExpiring7,    "انقضای نزدیک — ۷ روز مانده",    NotificationSeverity.Danger,   "۷ روز تا انقضای قرارداد باقی است"),
        (ContractExpiring1,    "انقضای نزدیک — فردا",           NotificationSeverity.Danger,   "فردا آخرین روز قرارداد است"),
        (ContractExpired,      "انقضای قرارداد",                NotificationSeverity.Danger,   "قرارداد منقضی شد"),
        (ContractRenewalDecision, "تصمیم تمدید قرارداد",         NotificationSeverity.Warning,  "قرارداد در شرف انقضا است و تصمیم تمدید گرفته نشده"),

        (ContractOverduePayment, "قسط معوق",                     NotificationSeverity.Danger,   "قسط پرداخت معوق شده"),
        (ContractPaymentReceived, "دریافت پرداخت",               NotificationSeverity.Success,  "پرداخت جدید ثبت شد"),

        (ContractStatusChanged,  "تغییر وضعیت قرارداد",          NotificationSeverity.Info,     "وضعیت قرارداد تغییر کرد"),

        (ContractApprovalRequested, "درخواست تایید قرارداد",     NotificationSeverity.Info,     "تایید قرارداد درخواست شد"),
        (ContractApprovalCompleted, "تکمیل تایید قرارداد",       NotificationSeverity.Success,  "فرآیند تایید قرارداد تکمیل شد"),
        (ContractApprovalDelegated, "تفویض تایید",               NotificationSeverity.Info,     "تایید به شما تفویض شد"),

        (ContractVersionCreated, "نسخه جدید قرارداد",            NotificationSeverity.Info,     "نسخه جدیدی از قرارداد ایجاد شد"),

        (ContractGuaranteeExpiry, "انقضای ضمانت‌نامه",           NotificationSeverity.Warning,  "ضمانت‌نامه قرارداد در حال انقضاست (stub — تا Item 22 غیرفعال)")
    ];
}