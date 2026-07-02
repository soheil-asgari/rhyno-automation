namespace OfficeAutomation.Modules.Finance.Domain;

public sealed class AccountGroup
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public AccountNature Nature { get; set; } = AccountNature.NoControl;
    public bool IsActive { get; set; } = true;
    public List<GeneralAccount> GeneralAccounts { get; set; } = [];
}
