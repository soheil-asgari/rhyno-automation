namespace OfficeAutomation.Modules.TaxEngine;

/// <summary>
/// Full breakdown of a tax calculation for an invoice.
/// </summary>
public sealed class TaxBreakdown
{
    /// <summary>Sum of all line-item amounts before tax and discounts.</summary>
    public decimal TotalBeforeTax { get; init; }

    /// <summary>Sum of all discount amounts (تخفیف).</summary>
    public decimal TotalDiscount { get; init; }

    /// <summary>Total amount subject to tax (مشمول مالیات).</summary>
    public decimal TaxableAmount { get; init; }

    /// <summary>Total amount exempt from tax (معاف از مالیات).</summary>
    public decimal ExemptAmount { get; init; }

    /// <summary>Applied VAT rate (e.g., 0.10 for 10%).</summary>
    public decimal VatRate { get; init; }

    /// <summary>Calculated VAT amount.</summary>
    public decimal TaxAmount { get; init; }

    /// <summary>Duty / Avarez amount (عوارض).</summary>
    public decimal DutyAmount { get; init; }

    /// <summary>Shipping charges.</summary>
    public decimal ShippingAmount { get; init; }

    /// <summary>Insurance charges.</summary>
    public decimal InsuranceAmount { get; init; }

    /// <summary>Final total (TotalBeforeTax - Discount + Tax + Duty + Shipping + Insurance).</summary>
    public decimal FinalAmount { get; init; }

    /// <summary>Whether any tax-exempt items were detected.</summary>
    public bool HasExemptItems { get; init; }

    /// <summary>Whether the entire invoice is tax-exempt.</summary>
    public bool IsFullyExempt { get; init; }

    /// <summary>Calculation timestamp.</summary>
    public DateTime CalculatedAt { get; init; } = DateTime.Now;

    /// <summary>Human-readable summary in Persian.</summary>
    public string SummaryPersian =>
        $"جمع قبل از مالیات: {TotalBeforeTax:N0} | " +
        $"تخفیف: {TotalDiscount:N0} | " +
        $"مشمول: {TaxableAmount:N0} | " +
        $"معاف: {ExemptAmount:N0} | " +
        $"مالیات ({VatRate * 100:0}%): {TaxAmount:N0} | " +
        $"عوارض: {DutyAmount:N0} | " +
        $"قابل پرداخت: {FinalAmount:N0}";
}
