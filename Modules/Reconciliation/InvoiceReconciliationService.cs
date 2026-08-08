using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OfficeAutomation.Modules.Persistence;
using OfficeAutomation.Modules.Persistence.Entities;

namespace OfficeAutomation.Modules.Reconciliation;

/// <summary>
/// Compares local <see cref="InvoiceRecord"/> data against Mowadiyan (سامانه مودیان)
/// submission responses and detects discrepancies such as amount mismatch, VAT rate
/// differences, economic-code mismatches, or missing Tax Reference IDs.
/// </summary>
public interface IInvoiceReconciliationService
{
    /// <summary>Runs reconciliation for a single invoice record.</summary>
    Task<InvoiceReconciliationResult> ReconcileAsync(Guid invoiceRecordId, CancellationToken cancellationToken);

    /// <summary>Runs reconciliation for all invoices that have a submission.</summary>
    Task<int> ReconcileAllAsync(CancellationToken cancellationToken);

    /// <summary>Returns the latest reconciliation result for an invoice.</summary>
    Task<InvoiceReconciliationResult?> GetLatestAsync(Guid invoiceRecordId, CancellationToken cancellationToken);
}

/// <inheritdoc />
public sealed class InvoiceReconciliationService : IInvoiceReconciliationService
{
    private readonly IDbContextFactory<TaxDbContext> _dbFactory;
    private readonly ILogger<InvoiceReconciliationService> _logger;

    public InvoiceReconciliationService(
        IDbContextFactory<TaxDbContext> dbFactory,
        ILogger<InvoiceReconciliationService> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    public async Task<InvoiceReconciliationResult?> GetLatestAsync(
        Guid invoiceRecordId, CancellationToken cancellationToken)
    {
        using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.InvoiceReconciliationResults
            .Include(r => r.Discrepancies)
            .Where(r => r.InvoiceRecordId == invoiceRecordId)
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<int> ReconcileAllAsync(CancellationToken cancellationToken)
    {
        using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var invoiceIds = await db.MowadiyanSubmissions
            .Where(s => s.Status == MowadiyanSubmissionStatus.Success)
            .Select(s => s.InvoiceRecordId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var count = 0;
        foreach (var id in invoiceIds)
        {
            await ReconcileAsync(id, cancellationToken);
            count++;
        }

        _logger.LogInformation("Reconciled {Count} invoices against Mowadiyan.", count);
        return count;
    }
public async Task<InvoiceReconciliationResult> ReconcileAsync(
        Guid invoiceRecordId, CancellationToken cancellationToken)
    {
        using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var invoice = await db.InvoiceRecords
            .Include(r => r.Seller)
            .Include(r => r.Buyer)
            .FirstOrDefaultAsync(r => r.Id == invoiceRecordId, cancellationToken);

        var submission = await db.MowadiyanSubmissions
            .Where(s => s.InvoiceRecordId == invoiceRecordId)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (invoice is null)
        {
            throw new InvalidOperationException($"Invoice record {invoiceRecordId} not found.");
        }

        var discrepancies = new List<ReconciliationDiscrepancy>();

        if (submission is null || submission.Status != MowadiyanSubmissionStatus.Success)
        {
            // Invoice was not successfully submitted → mark as NotSubmitted.
            return await SaveResultAsync(db, invoiceRecordId, submission?.Id,
                ReconciliationStatus.NotSubmitted, null, null, 0, discrepancies, cancellationToken);
        }

        var portal = ParsePortalValues(submission.ResponseJson);

        // 1. Missing Tax Reference ID (شماره پیگیری مالیاتی).
        if (string.IsNullOrWhiteSpace(submission.TaxReferenceId))
        {
            discrepancies.Add(new ReconciliationDiscrepancy
            {
                Type = "MissingTaxReferenceId",
                Description = "شماره پیگیری مالیاتی (Tax Reference ID) در پاسخ سامانه مودیان یافت نشد.",
                Suggestion = "استعلام مجدد وضعیت ارسال فاکتور از سامانه مودیان انجام شود."
            });
        }

        // 2. Amount mismatch (اختلاف مبلغ).
        if (portal?.TotalAmount is { } portalTotal &&
            Math.Abs(portalTotal - invoice.FinalAmount) > 1)
        {
            var diff = portalTotal - invoice.FinalAmount;
            discrepancies.Add(new ReconciliationDiscrepancy
            {
                Type = "AmountMismatch",
                LocalValue = invoice.FinalAmount.ToString("N0"),
                PortalValue = portalTotal.ToString("N0"),
                Description = $"اختلاف {Math.Abs(diff):N0} ریالی در مبلغ فاکتور با سامانه مودیان.",
                Suggestion = diff > 0
                    ? "مبلغ فاکتور محلی کمتر از سامانه مودیان است؛ مقادیر را بررسی کنید."
                    : "مبلغ فاکتور محلی بیشتر از سامانه مودیان است؛ مقادیر را بررسی کنید."
            });
        }

        // 3. VAT rate difference (اختلاف نرخ ارزش افزوده).
        if (portal?.VatRate is { } portalRate &&
            Math.Abs(portalRate - invoice.VatRate) > 0.0001m)
        {
            discrepancies.Add(new ReconciliationDiscrepancy
            {
                Type = "VatRateMismatch",
                LocalValue = (invoice.VatRate * 100).ToString("0.##") + "%",
                PortalValue = (portalRate * 100).ToString("0.##") + "%",
                Description = "نرخ مالیات بر ارزش افزوده با سامانه مودیان متفاوت است.",
                Suggestion = "نرخ مالیات بر ارزش افزوده را با نرخ مصوب سامانه مودیان هماهنگ کنید."
            });
        }

        // 4. VAT amount difference (اختلاف مبلغ ارزش افزوده).
        if (portal?.VatAmount is { } portalVat &&
            Math.Abs(portalVat - invoice.VatAmount) > 1)
        {
            var diffVat = portalVat - invoice.VatAmount;
            discrepancies.Add(new ReconciliationDiscrepancy
            {
                Type = "VatAmountMismatch",
                LocalValue = invoice.VatAmount.ToString("N0"),
                PortalValue = portalVat.ToString("N0"),
                Description = $"اختلاف {Math.Abs(diffVat):N0} ریالی در محاسبه ارزش افزوده.",
                Suggestion = "محاسبه ارزش افزوده را مجدداً انجام دهید."
            });
        }
// 5. Seller economic code mismatch.
        var sellerCode = invoice.Seller?.EconomicCode;
        if (!string.IsNullOrWhiteSpace(sellerCode) &&
            !string.IsNullOrWhiteSpace(portal?.SellerEconomicCode) &&
            !string.Equals(sellerCode, portal.SellerEconomicCode, StringComparison.Ordinal))
        {
            discrepancies.Add(new ReconciliationDiscrepancy
            {
                Type = "SellerEconomicCodeMismatch",
                LocalValue = sellerCode,
                PortalValue = portal.SellerEconomicCode,
                Description = "کد اقتصادی فروشنده با سامانه مودیان متفاوت است.",
                Suggestion = "کد اقتصادی فروشنده را اصلاح کنید."
            });
        }
        else if (!string.IsNullOrWhiteSpace(sellerCode) &&
                 portal?.SellerEconomicCode is null &&
                 !string.IsNullOrWhiteSpace(invoice.Seller?.Name))
        {
            discrepancies.Add(new ReconciliationDiscrepancy
            {
                Type = "SellerEconomicCodeMismatch",
                LocalValue = sellerCode,
                PortalValue = "(خالی)",
                Description = "کد اقتصادی فروشنده در سامانه مودیان ثبت نشده است.",
                Suggestion = "فرمت کد اقتصادی فروشنده را بررسی کنید و در سامانه مودیان ثبت نمایید."
            });
        }

        // 6. Buyer economic code validation.
        var buyerCode = invoice.Buyer?.EconomicCode;
        if (!string.IsNullOrWhiteSpace(buyerCode) && buyerCode.Length < 10)
        {
            discrepancies.Add(new ReconciliationDiscrepancy
            {
                Type = "BuyerEconomicCodeInvalid",
                LocalValue = buyerCode,
                PortalValue = portal?.BuyerEconomicCode,
                Description = "کد اقتصادی خریدار نامعتبر است.",
                Suggestion = "فرمت کد اقتصادی خریدار نامعتبر است؛ کد اقتصادی معتبر ۱۱ رقمی وارد کنید."
            });
        }
        else if (!string.IsNullOrWhiteSpace(buyerCode) &&
                 !string.IsNullOrWhiteSpace(portal?.BuyerEconomicCode) &&
                 !string.Equals(buyerCode, portal.BuyerEconomicCode, StringComparison.Ordinal))
        {
            discrepancies.Add(new ReconciliationDiscrepancy
            {
                Type = "BuyerEconomicCodeMismatch",
                LocalValue = buyerCode,
                PortalValue = portal.BuyerEconomicCode,
                Description = "کد اقتصادی خریدار با سامانه مودیان متفاوت است.",
                Suggestion = "کد اقتصادی خریدار را با سامانه مودیان هماهنگ کنید."
            });
        }

        var status = discrepancies.Count == 0
            ? ReconciliationStatus.Matched
            : ReconciliationStatus.Mismatched;

        var result = await SaveResultAsync(db, invoiceRecordId, submission.Id,
            status, submission.TaxReferenceId, portal, discrepancies.Count, discrepancies, cancellationToken);

        _logger.LogInformation(
            "Reconciliation for invoice {Id}: {Status} with {Count} discrepancies.",
            invoiceRecordId, status, discrepancies.Count);

        return result;
    }
private async Task<InvoiceReconciliationResult> SaveResultAsync(
        TaxDbContext db,
        Guid invoiceRecordId,
        Guid? submissionId,
        ReconciliationStatus status,
        string? taxReferenceId,
        PortalValues? portal,
        int discrepancyCount,
        List<ReconciliationDiscrepancy> discrepancies,
        CancellationToken cancellationToken)
    {
        var result = new InvoiceReconciliationResult
        {
            InvoiceRecordId = invoiceRecordId,
            SubmissionId = submissionId,
            Status = status,
            TaxReferenceId = taxReferenceId,
            PortalTotalAmount = portal?.TotalAmount,
            PortalVatAmount = portal?.VatAmount,
            PortalVatRate = portal?.VatRate,
            PortalSellerEconomicCode = portal?.SellerEconomicCode,
            PortalBuyerEconomicCode = portal?.BuyerEconomicCode,
            DiscrepancyCount = discrepancies.Count,
            Discrepancies = discrepancies
        };

        db.InvoiceReconciliationResults.Add(result);
        await db.SaveChangesAsync(cancellationToken);
        return result;
    }

    private static PortalValues? ParsePortalValues(string? responseJson)
    {
        if (string.IsNullOrWhiteSpace(responseJson))
        {
            return null;
        }

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(responseJson);
            var root = doc.RootElement;
            var result = new PortalValues();

            if (root.TryGetProperty("result", out var resultEl))
            {
                if (resultEl.TryGetProperty("fiscalAmount", out var fa))
                {
                    result.TotalAmount = fa.GetDecimal();
                }
                if (resultEl.TryGetProperty("totalAmount", out var ta))
                {
                    result.TotalAmount = ta.GetDecimal();
                }
            }

            return result;
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    private sealed class PortalValues
    {
        public decimal? TotalAmount { get; set; }
        public decimal? VatAmount { get; set; }
        public decimal? VatRate { get; set; }
        public string? SellerEconomicCode { get; set; }
        public string? BuyerEconomicCode { get; set; }
    }
}