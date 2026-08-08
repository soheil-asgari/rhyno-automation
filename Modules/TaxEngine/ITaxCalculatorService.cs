using OfficeAutomation.Modules.InvoiceStructuring;

namespace OfficeAutomation.Modules.TaxEngine;

/// <summary>
/// Calculates VAT (مالیات بر ارزش افزوده) for Iranian invoices with the
/// standard 10% rate, handling exempt items (معاف) and taxable totals (مشمول).
/// </summary>
public interface ITaxCalculatorService
{
    /// <summary>
    /// Calculates a tax breakdown from classified line items.
    /// </summary>
    /// <param name="classifiedSummary">Classification results from ItemizedTaxClassifier.</param>
    /// <param name="vatRate">Optional override VAT rate (default 0.10 = 10%).</param>
    Task<TaxBreakdown> CalculateAsync(ItemizedTaxClassifier.ClassifiedSummary classifiedSummary, decimal? vatRate = null);

    /// <summary>
    /// Calculates a tax breakdown from raw line-item strings (auto-classifies them first).
    /// </summary>
    Task<TaxBreakdown> CalculateFromLinesAsync(IEnumerable<string> lines, decimal? vatRate = null);

    /// <summary>
    /// Recalculates tax for already-known amounts.
    /// </summary>
    Task<TaxBreakdown> CalculateSimpleAsync(
        decimal totalBeforeTax,
        decimal totalDiscount,
        decimal exemptItemAmount,
        decimal? extraDuty = null,
        decimal? extraShipping = null,
        decimal? extraInsurance = null,
        decimal? vatRate = null);
}
