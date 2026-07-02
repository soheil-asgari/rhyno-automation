namespace OfficeAutomation.Modules.Finance.Domain;

public sealed class GeneralAccount
{
    public int Id { get; set; }
    public int AccountGroupId { get; set; }
    public AccountGroup AccountGroup { get; set; } = null!;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public AccountNature Nature { get; set; } = AccountNature.NoControl;
    public bool IsActive { get; set; } = true;
    public List<SubsidiaryAccount> SubsidiaryAccounts { get; set; } = [];
}
