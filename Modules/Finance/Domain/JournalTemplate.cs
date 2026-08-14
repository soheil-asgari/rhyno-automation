namespace OfficeAutomation.Modules.Finance.Domain;

/// <summary>
/// A reusable accounting voucher template that generates actual valid vouchers (Section 11).
/// </summary>
public sealed class JournalTemplate
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int JournalTypeId { get; set; }
    public JournalType JournalType { get; set; } = null!;
    public bool IsRecurring { get; set; }
    /// <summary>Number of periods between recurring runs. Ignored when <see cref="IsRecurring"/> is false.</summary>
    public int? RecurrenceEveryMonths { get; set; }
    /// <summary>Next scheduled run date for a recurring template.</summary>
    public DateTime? NextRunDate { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Reference { get; set; }
    public List<JournalTemplateLine> Lines { get; set; } = [];
}

public sealed class JournalTemplateLine
{
    public int Id { get; set; }
    public int JournalTemplateId { get; set; }
    public JournalTemplate JournalTemplate { get; set; } = null!;
    public int SubsidiaryAccountId { get; set; }
    public SubsidiaryAccount SubsidiaryAccount { get; set; } = null!;
    public Guid? FloatingDetailAccountId { get; set; }
    public FloatingDetailAccount? FloatingDetailAccount { get; set; }
    public decimal DebitAmount { get; set; }
    public decimal CreditAmount { get; set; }
    public string? Narration { get; set; }
    public int DisplayOrder { get; set; }
}