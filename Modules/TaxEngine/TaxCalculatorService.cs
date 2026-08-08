using Microsoft.Extensions.Logging;
using OfficeAutomation.Modules.InvoiceStructuring;

namespace OfficeAutomation.Modules.TaxEngine;

/// <summary>
/// Iranian VAT calculator implementing the standard 10% tax rate
/// with exempt-item detection and full financial breakdown.
/// </summary>
public sealed class TaxCalculatorService : ITaxCalculatorService
{
    /// <summary>Standard Iranian VAT rate (10%).</summary>
    public const decimal DefaultVatRate = 0.10m;

    private readonly ILogger<TaxCalculatorService> _logger;

    public TaxCalculatorService(ILogger<TaxCalculatorService> logger)
    {
        _logger = logger;
    }

    public Task<TaxBreakdown> CalculateAsync(
        ItemizedTaxClassifier.ClassifiedSummary classified,
        decimal? vatRate = null)
    {
        var rate = vatRate ?? DefaultVatRate;

        var totalBeforeTax = classified.TotalItemAmount;
        var totalDiscount = classified.TotalDiscount;
        var exemptAmount = classified.Items
            .Where(i => i.IsTaxExempt)
            .Sum(i => i.Amount ?? 0);
        var taxableAmount = totalBeforeTax - exemptAmount - totalDiscount;
        if (taxableAmount < 0) taxableAmount = 0;

        var taxAmount = Math.Round(taxableAmount * rate, 2);
        var dutyAmount = classified.TotalDuty;
        var shippingAmount = classified.TotalShipping;
        var insuranceAmount = classified.TotalInsurance;

        var finalAmount = totalBeforeTax - totalDiscount + taxAmount + dutyAmount + shippingAmount + insuranceAmount;
        if (finalAmount < 0) finalAmount = 0;

        var hasExempt = classified.Items.Any(i => i.IsTaxExempt);
        var isFullyExempt = hasExempt && taxableAmount == 0;

        var breakdown = new TaxBreakdown
        {
            TotalBeforeTax = totalBeforeTax,
            TotalDiscount = totalDiscount,
            TaxableAmount = taxableAmount,
            ExemptAmount = exemptAmount,
            VatRate = rate,
            TaxAmount = taxAmount,
            DutyAmount = dutyAmount,
            ShippingAmount = shippingAmount,
            InsuranceAmount = insuranceAmount,
            FinalAmount = finalAmount,
            HasExemptItems = hasExempt,
            IsFullyExempt = isFullyExempt,
            CalculatedAt = DateTime.Now
        };

        _logger.LogInformation(
            "Tax calculated: BeforeTax={BeforeTax}, Discount={Discount}, Taxable={Taxable}, Tax={Tax}, Final={Final}",
            breakdown.TotalBeforeTax, breakdown.TotalDiscount,
            breakdown.TaxableAmount, breakdown.TaxAmount, breakdown.FinalAmount);

        return Task.FromResult(breakdown);
    }

    public async Task<TaxBreakdown> CalculateFromLinesAsync(
        IEnumerable<string> lines, decimal? vatRate = null)
    {
        var classified = ItemizedTaxClassifier.ClassifyBatch(lines);
        return await CalculateAsync(classified, vatRate);
    }

    public Task<TaxBreakdown> CalculateSimpleAsync(
        decimal totalBeforeTax,
        decimal totalDiscount,
        decimal exemptItemAmount,
        decimal? extraDuty = null,
        decimal? extraShipping = null,
        decimal? extraInsurance = null,
        decimal? vatRate = null)
    {
        var rate = vatRate ?? DefaultVatRate;
        var taxableAmount = totalBeforeTax - exemptItemAmount - totalDiscount;
        if (taxableAmount < 0) taxableAmount = 0;

        var taxAmount = Math.Round(taxableAmount * rate, 2);
        var duty = extraDuty ?? 0;
        var shipping = extraShipping ?? 0;
        var insurance = extraInsurance ?? 0;

        var finalAmount = totalBeforeTax - totalDiscount + taxAmount + duty + shipping + insurance;
        if (finalAmount < 0) finalAmount = 0;

        var breakdown = new TaxBreakdown
        {
            TotalBeforeTax = totalBeforeTax,
            TotalDiscount = totalDiscount,
            TaxableAmount = taxableAmount,
            ExemptAmount = exemptItemAmount,
            VatRate = rate,
            TaxAmount = taxAmount,
            DutyAmount = duty,
            ShippingAmount = shipping,
            InsuranceAmount = insurance,
            FinalAmount = finalAmount,
            HasExemptItems = exemptItemAmount > 0,
            IsFullyExempt = exemptItemAmount > 0 && taxableAmount == 0,
            CalculatedAt = DateTime.Now
        };

        return Task.FromResult(breakdown);
    }
}
