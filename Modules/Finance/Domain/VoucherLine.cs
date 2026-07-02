using System.ComponentModel.DataAnnotations.Schema;

namespace OfficeAutomation.Modules.Finance.Domain;

public sealed class VoucherLine
{
    public int Id { get; set; }

    public int VoucherHeaderId { get; set; }

    public VoucherHeader VoucherHeader { get; set; } = null!;

    public int SubsidiaryAccountId { get; set; }

    public SubsidiaryAccount SubsidiaryAccount { get; set; } = null!;

    public int? DetailedAccountId { get; set; }

    public DetailedAccount? DetailedAccount { get; set; }

    public Guid? FloatingDetailAccountId { get; set; }

    public FloatingDetailAccount? FloatingDetailAccount { get; set; }

    public int? CostCenterId { get; set; }

    public Guid? CurrencyId { get; set; }

    public Currency? Currency { get; set; }

    public decimal ExchangeRate { get; set; } = 1m;

    [NotMapped]
    public decimal CurrencyRate
    {
        get => ExchangeRate;
        set => ExchangeRate = value;
    }

    public decimal? ForeignAmount { get; set; }

    public decimal DebitAmount { get; set; }

    public decimal CreditAmount { get; set; }

    public string? Narration { get; set; }

    public int DisplayOrder { get; set; }
}
