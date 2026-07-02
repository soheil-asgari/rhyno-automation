namespace OfficeAutomation.Modules.Finance.Domain;

public sealed class Currency
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public bool IsBaseCurrency { get; set; }
    public bool IsActive { get; set; } = true;
    public List<CurrencyExchangeRate> ExchangeRates { get; set; } = [];
    public List<VoucherLine> VoucherLines { get; set; } = [];
}
