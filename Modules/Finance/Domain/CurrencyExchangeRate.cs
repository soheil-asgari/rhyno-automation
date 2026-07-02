namespace OfficeAutomation.Modules.Finance.Domain;

public sealed class CurrencyExchangeRate
{
    public int Id { get; set; }
    public Guid CurrencyId { get; set; }
    public Currency Currency { get; set; } = null!;
    public DateTime RateDate { get; set; }
    public decimal BuyRate { get; set; }
    public decimal SellRate { get; set; }
}
