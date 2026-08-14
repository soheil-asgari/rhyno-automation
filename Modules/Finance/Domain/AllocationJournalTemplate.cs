namespace OfficeAutomation.Modules.Finance.Domain;

/// <summary>
/// An allocation journal template distributes a total amount across multiple target
/// accounts based on fixed percentages or dynamic ratios (Section 11 - Allocation Journals).
/// </summary>
public sealed class AllocationJournalTemplate
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>
    /// The subsidiary account from which the total amount is drawn (source/overhead account).
    /// </summary>
    public int SourceSubsidiaryAccountId { get; set; }

    public SubsidiaryAccount SourceSubsidiaryAccount { get; set; } = null!;

    /// <summary>
    /// Total amount to be allocated across target lines.
    /// </summary>
    public decimal TotalAmount { get; set; }

    public int JournalTypeId { get; set; }

    public JournalType JournalType { get; set; } = null!;

    public bool IsActive { get; set; } = true;

    public List<AllocationJournalTemplateLine> Lines { get; set; } = [];
}

/// <summary>
/// A single allocation line defining how much of the total amount goes to a target account.
/// </summary>
public sealed class AllocationJournalTemplateLine
{
    public int Id { get; set; }

    public int AllocationJournalTemplateId { get; set; }

    public AllocationJournalTemplate AllocationJournalTemplate { get; set; } = null!;

    /// <summary>
    /// The subsidiary account receiving the allocated amount.
    /// </summary>
    public int TargetSubsidiaryAccountId { get; set; }

    public SubsidiaryAccount TargetSubsidiaryAccount { get; set; } = null!;

    /// <summary>
    /// Fixed percentage (0-100) of the total amount to allocate to this target.
    /// Mutually exclusive with Ratio.
    /// </summary>
    public decimal? Percentage { get; set; }

    /// <summary>
    /// Dynamic ratio weight. The actual amount is TotalAmount * (Ratio / SumOfAllRatios).
    /// Mutually exclusive with Percentage.
    /// </summary>
    public decimal? Ratio { get; set; }

    /// <summary>
    /// Optional narration for the generated voucher line.
    /// </summary>
    public string? Narration { get; set; }

    public int DisplayOrder { get; set; }
}