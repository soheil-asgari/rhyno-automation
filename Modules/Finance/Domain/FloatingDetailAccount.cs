namespace OfficeAutomation.Modules.Finance.Domain;

public sealed class FloatingDetailAccount
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public FloatingDetailAccountType Type { get; set; }
    public bool IsActive { get; set; } = true;
    public List<SubsidiaryAccountFloatingDetail> SubsidiaryAccountLinks { get; set; } = [];
    public List<VoucherLine> VoucherLines { get; set; } = [];
}
