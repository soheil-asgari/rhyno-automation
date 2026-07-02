namespace OfficeAutomation.Modules.Finance.Domain;

public sealed class DetailedAccount
{
    public int Id { get; set; }
    public int? SubsidiaryAccountId { get; set; }
    public SubsidiaryAccount? SubsidiaryAccount { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? PartyType { get; set; }
    public string? ExternalReference { get; set; }
    public bool IsFloating { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public List<VoucherLine> VoucherLines { get; set; } = [];
}
