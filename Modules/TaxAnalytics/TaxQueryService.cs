using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OfficeAutomation.Modules.AiEngine;
using OfficeAutomation.Modules.Persistence;

namespace OfficeAutomation.Modules.TaxAnalytics;

/// <inheritdoc />
public sealed class TaxQueryService : ITaxQueryService
{
    private readonly IDbContextFactory<TaxDbContext> _dbFactory;
    private readonly IEntityResolutionService _entityResolution;
    private readonly ILogger<TaxQueryService> _logger;

    public TaxQueryService(
        IDbContextFactory<TaxDbContext> dbFactory,
        IEntityResolutionService entityResolution,
        ILogger<TaxQueryService> logger)
    {
        _dbFactory = dbFactory;
        _entityResolution = entityResolution;
        _logger = logger;
    }

    public async Task<TaxQueryResult> AnswerAsync(string question, CancellationToken cancellationToken)
    {
        var q = (question ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(q))
        {
            return new TaxQueryResult { Success = false, Answer = "سوال خود را وارد کنید." };
        }

        try
        {
            var isPurchase = ContainsAny(q, "خرید", "فروشندگان", "خریدار", "پرداخت");
            var isSale = ContainsAny(q, "فروش", "درآمد");
            var isCount = ContainsAny(q, "تعداد", "چند فاکتور", "چندتا");
            var isTax = ContainsAny(q, "مالیات", "ارزش افزوده");
            var isThisMonth = ContainsAny(q, "این ماه", "ماه جاری", "ماه گذشته");
            var isLastMonth = ContainsAny(q, "ماه گذشته", "ماه قبل");

            var partyName = ExtractPartyName(q);
            var partyType = isPurchase ? "Buyer" : "Seller";

            using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

            // Build the invoice query.
            var query = db.InvoiceRecords.AsNoTracking().AsQueryable();

            if (isPurchase)
            {
                query = query.Where(r => r.InvoiceType == "Purchase");
            }
            else if (isSale)
            {
                query = query.Where(r => r.InvoiceType == "Sale");
            }

            var now = DateTime.Now;
            if (isThisMonth)
            {
                var monthStart = new DateTime(now.Year, now.Month, 1);
                query = query.Where(r => r.InvoiceDate >= monthStart);
            }
            else if (isLastMonth)
            {
                var monthStart = new DateTime(now.Year, now.Month, 1);
                var lastMonthStart = monthStart.AddMonths(-1);
                query = query.Where(r => r.InvoiceDate >= lastMonthStart && r.InvoiceDate < monthStart);
            }

            // Resolve a party and filter by its canonical entity when a name is present.
            Guid? partyId = null;
            string? resolvedName = null;
            if (!string.IsNullOrWhiteSpace(partyName))
            {
                var resolved = partyType == "Seller"
                    ? await _entityResolution.ResolveSellerAsync(partyName, cancellationToken)
                    : await _entityResolution.ResolveBuyerAsync(partyName, cancellationToken);

                if (resolved.IsMatched)
                {
                    partyId = resolved.EntityId;
                    resolvedName = resolved.CanonicalName;
                    _logger.LogInformation("Resolved party '{Party}' to '{Canonical}' for query.", partyName, resolved.CanonicalName);
                }
            }

            if (partyId.HasValue)
            {
                query = partyType == "Seller"
                    ? query.Where(r => r.SellerId == partyId.Value)
                    : query.Where(r => r.BuyerId == partyId.Value);
            }

            var invoices = await query.ToListAsync(cancellationToken);

            var totalCount = invoices.Count;
            var totalAmount = invoices.Sum(r => r.FinalAmount);
            var totalTax = invoices.Sum(r => r.VatAmount);
if (isCount)
            {
                return new TaxQueryResult
                {
                    Success = true,
                    Answer = $"تعداد {totalCount} فاکتور" + (resolvedName is null ? "" : $" از {resolvedName}") + " یافت شد.",
                    DataPoints = { new TaxQueryDataPoint { Label = "تعداد فاکتور", Value = totalCount.ToString("N0") } }
                };
            }

            if (isTax)
            {
                return new TaxQueryResult
                {
                    Success = true,
                    Answer = $"مجموع مالیات" + (resolvedName is null ? "" : $" از {resolvedName}") +
                             $" برابر {totalTax:N0} ریال است.",
                    DataPoints = { new TaxQueryDataPoint { Label = "مجموع مالیات", Value = totalTax.ToString("N0"), Unit = "ریال" } }
                };
            }

            return new TaxQueryResult
            {
                Success = true,
                Answer = $"مجموع" + (resolvedName is null ? "" : $" خرید از {resolvedName}") +
                         $" برابر {totalAmount:N0} ریال است.",
                DataPoints =
                {
                    new TaxQueryDataPoint { Label = "تعداد فاکتور", Value = totalCount.ToString("N0") },
                    new TaxQueryDataPoint { Label = "مبلغ کل", Value = totalAmount.ToString("N0"), Unit = "ریال" },
                    new TaxQueryDataPoint { Label = "مالیات", Value = totalTax.ToString("N0"), Unit = "ریال" }
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to answer tax query: {Question}", question);
            return new TaxQueryResult { Success = false, Answer = "خطا در پاسخ‌گویی به سوال. لطفاً دوباره تلاش کنید." };
        }
    }

    private static string ExtractPartyName(string question)
    {
        // Look for a phrase after known prepositions/keywords such as "از", "به", "برای".
        var fromIdx = question.LastIndexOf(" از ", StringComparison.Ordinal);
        if (fromIdx < 0)
        {
            fromIdx = question.LastIndexOf("از ", StringComparison.Ordinal);
        }

        if (fromIdx < 0)
        {
            return string.Empty;
        }

        var after = question[(fromIdx + (question[fromIdx] == ' ' ? 4 : 3))..].Trim();
        // Cut at the first question word or period.
        foreach (var stop in new[] { "؟", "? ", " در ", "طی ", " برای " })
        {
            var idx = after.IndexOf(stop, StringComparison.Ordinal);
            if (idx > 0)
            {
                after = after[..idx];
                break;
            }
        }

        return after.Trim();
    }

    private static bool ContainsAny(string text, params string[] needles)
    {
        foreach (var needle in needles)
        {
            if (text.Contains(needle, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}