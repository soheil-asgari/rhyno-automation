namespace OfficeAutomation.Modules.Finance.Domain;

public sealed class SubsidiaryAccountFloatingDetail
{
    public int SubsidiaryAccountId { get; set; }
    public SubsidiaryAccount SubsidiaryAccount { get; set; } = null!;
    public Guid FloatingDetailAccountId { get; set; }
    public FloatingDetailAccount FloatingDetailAccount { get; set; } = null!;
}
