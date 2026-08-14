namespace OfficeAutomation.Modules.Finance.Domain;

public sealed class VoucherHeader
{
    public int Id { get; set; }

    public int SequenceNumber { get; set; }

    public int? VoucherNumber { get; set; }

    public string DocumentNumber { get; set; } = string.Empty;

    public DateTime VoucherDate { get; set; }

    public string? Description { get; set; }

    /// <summary>
    /// Optional external reference for the voucher (e.g. source document number).
    /// </summary>
    public string? Reference { get; set; }

    public VoucherStatus Status { get; set; } = VoucherStatus.Draft;

    public string PostingStatus { get; set; } = Domain.PostingStatus.Draft;

    /// <summary>
    /// When true the voucher is a memo / draft that must NOT affect general ledger,
    /// subsidiary ledger, party balances, trial balance, or closing calculations.
    /// Memo vouchers remain searchable and auditable.
    /// </summary>
    public bool IsMemo { get; set; }

    /// <summary>User that created the voucher.</summary>
    public string? IssuedBy { get; set; }

    /// <summary>Creation timestamp of the voucher.</summary>
    public DateTimeOffset IssuedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>User that last modified the voucher.</summary>
    public string? LastModifiedBy { get; set; }

    /// <summary>Last modification timestamp of the voucher.</summary>
    public DateTimeOffset? LastModifiedAt { get; set; }

    public decimal TotalDebits { get; set; }

    public decimal TotalCredits { get; set; }

    public int FiscalYearId { get; set; }

    public FiscalYear FiscalYear { get; set; } = null!;

    public int JournalTypeId { get; set; }

    public JournalType JournalType { get; set; } = null!;

    public int? ReversalOfVoucherHeaderId { get; set; }

    public VoucherHeader? ReversalOfVoucherHeader { get; set; }

    public int? ReversedByVoucherHeaderId { get; set; }

    public VoucherHeader? ReversedByVoucherHeader { get; set; }

    public List<VoucherLine> Lines { get; set; } = [];

    public void EnsureMutable()
    {
        if (Status == VoucherStatus.Permanent)
        {
            throw new InvalidOperationException("Permanent accounting vouchers are immutable.");
        }
    }

    public void ChangeStatus(VoucherStatus newStatus)
    {
        if (Status == newStatus)
        {
            return;
        }

        if (Status == VoucherStatus.Permanent)
        {
            throw new InvalidOperationException("Permanent accounting vouchers cannot move to another status.");
        }

        var isValidTransition =
            (Status == VoucherStatus.Draft && newStatus is VoucherStatus.Reviewed or VoucherStatus.Posted) ||
            (Status == VoucherStatus.Reviewed && newStatus == VoucherStatus.Approved) ||
            (Status == VoucherStatus.Approved && newStatus is VoucherStatus.Permanent or VoucherStatus.Posted) ||
            (Status == VoucherStatus.Posted && newStatus is VoucherStatus.Permanent or VoucherStatus.Approved) ||
            (Status == VoucherStatus.Posted && newStatus == VoucherStatus.Reversed);

        if (!isValidTransition)
        {
            throw new InvalidOperationException($"Invalid voucher status transition: {Status} -> {newStatus}.");
        }

        Status = newStatus;
    }

    /// <summary>
    /// Marks progress on the voucher lifecycle. Progressing towards the posted state
    /// (Reviewed / Approved / Posted / Permanent) sets the posting status accordingly.
    /// </summary>
    public void Progress(VoucherStatus newStatus)
    {
        ChangeStatus(newStatus);

        if (newStatus == VoucherStatus.Reversed)
        {
            PostingStatus = Domain.PostingStatus.Reversed;
            return;
        }

        if (newStatus is VoucherStatus.Approved or VoucherStatus.Posted or VoucherStatus.Permanent)
        {
            PostingStatus = newStatus == VoucherStatus.Permanent
                ? Domain.PostingStatus.Posted
                : Domain.PostingStatus.Approved;
        }
    }

    public bool IsBalanced()
    {
        return TotalDebits == TotalCredits;
    }
}