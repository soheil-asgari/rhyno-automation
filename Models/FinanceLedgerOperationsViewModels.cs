using Microsoft.AspNetCore.Mvc.Rendering;
using OfficeAutomation.Modules.Finance.Domain;

namespace OfficeAutomation.Models;

public sealed class FinanceLedgerOperationsVM
{
    public IReadOnlyList<FiscalPeriodRowVM> FiscalPeriods { get; set; } = [];
    public IReadOnlyList<CurrencyRateRowVM> ExchangeRates { get; set; } = [];
    public IReadOnlyList<VoucherGridRowVM> Vouchers { get; set; } = [];
    public IReadOnlyList<string> VisibleVoucherColumns { get; set; } = [];
    public Guid? CurrentFiscalPeriodId { get; set; }
    public List<SelectListItem> CurrencyOptions { get; set; } = [];
    public List<SelectListItem> CurrencyLookupOptions { get; set; } = [];
    public List<SelectListItem> AccountOptions { get; set; } = [];
    public List<SelectListItem> JournalTypeOptions { get; set; } = [];
    public SimpleVoucherCreateVM Voucher { get; set; } = new();
    public CurrencyExchangeRateCreateVM ExchangeRate { get; set; } = new();
    public PeriodClosingRequestVM PeriodClosing { get; set; } = new();
}

public sealed class FiscalPeriodRowVM
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int PeriodNumber { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Status { get; set; } = string.Empty;
}

public sealed class CurrencyRateRowVM
{
    public string CurrencyCode { get; set; } = string.Empty;
    public DateTime RateDate { get; set; }
    public decimal BuyRate { get; set; }
    public decimal SellRate { get; set; }
}

public sealed class VoucherGridRowVM
{
    public int Id { get; set; }
    public int SequenceNumber { get; set; }
    public int? VoucherNumber { get; set; }
    public DateTime VoucherDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public string JournalType { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public VoucherStatus Status { get; set; }
    public string PostingStatus { get; set; } = string.Empty;
}

public sealed class CurrencyExchangeRateCreateVM
{
    public Guid CurrencyId { get; set; }
    public DateTime RateDate { get; set; } = DateTime.Today;
    public decimal BuyRate { get; set; }
    public decimal SellRate { get; set; }
}

public sealed class SimpleVoucherCreateVM
{
    public DateTime VoucherDate { get; set; } = DateTime.Today;
    public int JournalTypeId { get; set; }
    public string Description { get; set; } = string.Empty;
    public List<SimpleVoucherLineVM> Lines { get; set; } =
    [
        new(),
        new()
    ];
}

public sealed class SimpleVoucherLineVM
{
    public int SubsidiaryAccountId { get; set; }
    public Guid? FloatingDetailAccountId { get; set; }
    public Guid? CurrencyId { get; set; }
    public decimal ExchangeRate { get; set; } = 1m;
    public decimal? ForeignAmount { get; set; }
    public decimal DebitAmount { get; set; }
    public decimal CreditAmount { get; set; }
    public string? Narration { get; set; }
}

public sealed class ChangeVoucherStatusRequest
{
    public int VoucherId { get; set; }
    public string TargetStatus { get; set; } = string.Empty;
}

public sealed class PeriodClosingRequestVM
{
    public Guid FiscalPeriodId { get; set; }
    public int DestinationAccountId { get; set; }
}
