namespace OfficeAutomation.Modules.Finance.Domain;

public sealed class SubsidiaryAccount
{
    public int Id { get; set; }
    public int GeneralAccountId { get; set; }
    public GeneralAccount GeneralAccount { get; set; } = null!;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string SystemKey { get; set; } = string.Empty;
    public AccountNature Nature { get; set; } = AccountNature.NoControl;
    public bool IsActive { get; set; } = true;
    public bool IsTemporary { get; set; }
    public bool AllowsFloatingDetail { get; set; } = true;
    public List<DetailedAccount> DetailedAccounts { get; set; } = [];
    public List<SubsidiaryAccountFloatingDetail> FloatingDetailLinks { get; set; } = [];
    public List<VoucherLine> VoucherLines { get; set; } = [];
}
