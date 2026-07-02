namespace OfficeAutomation.Modules.Finance.Domain;

public sealed class VoucherHeader
{
    public int Id { get; set; }

    public int SequenceNumber { get; set; }

    public int? VoucherNumber { get; set; }

    public string DocumentNumber { get; set; } = string.Empty;

    public DateTime VoucherDate { get; set; }

    public string? Description { get; set; }

    public VoucherStatus Status { get; set; } = VoucherStatus.Draft;

    public string PostingStatus { get; set; } = Domain.PostingStatus.Draft;

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
            (Status == VoucherStatus.Draft && newStatus == VoucherStatus.Reviewed) ||
            (Status == VoucherStatus.Reviewed && newStatus == VoucherStatus.Approved) ||
            (Status == VoucherStatus.Approved && newStatus == VoucherStatus.Permanent);

        if (!isValidTransition)
        {
            throw new InvalidOperationException($"Invalid voucher status transition: {Status} -> {newStatus}.");
        }

        Status = newStatus;
    }

    public bool IsBalanced()
    {
        return TotalDebits == TotalCredits;
    }
}
