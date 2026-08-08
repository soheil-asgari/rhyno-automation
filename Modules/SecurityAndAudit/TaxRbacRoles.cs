namespace OfficeAutomation.Modules.SecurityAndAudit;

/// <summary>
/// Central RBAC role definitions for the tax &amp; audit modules. These constants are
/// used with <c>[Authorize(Roles = ...)]</c> attributes so that fine-grained
/// authorization can be applied per endpoint for the Admin, Accountant and Auditor roles.
/// </summary>
public static class TaxRbacRoles
{
    public const string Admin = "Admin";
    public const string Accountant = "Accountant";
    public const string Auditor = "Auditor";

    /// <summary>Roles that can view tax analytics and insights.</summary>
    public const string AnalyticsViewers = "Admin,Accountant,Auditor";

    /// <summary>Roles that can trigger exports and run submissions.</summary>
    public const string Operations = "Admin,Accountant";

    /// <summary>Roles that can read the audit trail.</summary>
    public const string AuditReaders = "Admin,Auditor";
}

/// <summary>
/// Well-known audit action identifiers used when recording explicit tax audit entries.
/// </summary>
public static class TaxAuditActions
{
    public const string SubmissionCreated = "TaxSubmissionCreated";
    public const string SubmissionSubmitted = "TaxSubmissionSubmitted";
    public const string Exported = "TaxDataExported";
    public const string Recalculated = "TaxRecalculated";
    public const string InsightGenerated = "TaxInsightGenerated";
    public const string WebhookReceived = "WebhookIntakeReceived";
    public const string EntityResolved = "TaxEntityResolved";
}