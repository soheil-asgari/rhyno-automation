namespace OfficeAutomation.Modules.Finance.Domain;

public sealed class JournalType
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public List<VoucherHeader> Vouchers { get; set; } = [];
}
