namespace OfficeAutomation.Services.Auditing;

public sealed class AuditRetentionOptions
{
    public const string SectionName = "AuditRetention";

    public bool Enabled { get; set; }
    public int RetainDays { get; set; } = 180;
    public int BatchSize { get; set; } = 500;
    public string ArchivePath { get; set; } = "logs/audit-archive";
}
