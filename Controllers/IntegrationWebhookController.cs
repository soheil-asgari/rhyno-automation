using OfficeAutomation.Services.Licensing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Modules.Persistence;
using OfficeAutomation.Modules.Persistence.Entities;
using OfficeAutomation.Services.Security;

namespace OfficeAutomation.Controllers;

/// <summary>
/// Standard REST endpoints for syncing invoice &amp; tax data with external ERP
/// systems (e.g. Sepidar, Rahkaran). All endpoints are secured via API Key.
/// </summary>
[ApiController]
[Route("api/v1/integration")]
[EnableRateLimiting("api")]
[LicenseExempt]
public sealed class IntegrationWebhookController : ControllerBase
{
    private readonly IDbContextFactory<TaxDbContext> _dbFactory;
    private readonly ILogger<IntegrationWebhookController> _logger;

    public IntegrationWebhookController(
        IDbContextFactory<TaxDbContext> dbFactory,
        ILogger<IntegrationWebhookController> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    /// <summary>Returns a batching-friendly list of invoices for ERP sync.</summary>
    [HttpGet("invoices")]
    [Authorize(AuthenticationSchemes = ApiKeyAuthenticationExtensions.AuthenticationScheme)]
    [Produces("application/json")]
    public async Task<IActionResult> GetInvoicesForSync(
        [FromQuery] DateTime? since = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 500);

        using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var query = db.InvoiceRecords
            .Include(r => r.Seller)
            .Include(r => r.Buyer)
            .AsNoTracking()
            .AsQueryable();

        if (since.HasValue)
        {
            query = query.Where(r => r.UpdatedAt >= since.Value);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(r => r.UpdatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new
            {
                id = r.Id,
                invoiceNumber = r.InvoiceNumber,
                dateShamsi = r.DateShamsi,
                invoiceType = r.InvoiceType,
                status = r.Status,
                totalAmount = r.TotalAmount,
                vatAmount = r.VatAmount,
                dutyAmount = r.DutyAmount,
                finalAmount = r.FinalAmount,
                seller = r.Seller != null ? r.Seller.Name : null,
                buyer = r.Buyer != null ? r.Buyer.Name : null,
            })
            .ToListAsync(cancellationToken);

        return Ok(new { success = true, total, page, pageSize, items });
    }

    /// <summary>Returns the Mowadiyan submission statuses for ERP sync.</summary>
    [HttpGet("submissions")]
    [Authorize(AuthenticationSchemes = ApiKeyAuthenticationExtensions.AuthenticationScheme)]
    [Produces("application/json")]
    public async Task<IActionResult> GetSubmissionsForSync(
        [FromQuery] DateTime? since = null,
        CancellationToken cancellationToken = default)
    {
        using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var query = db.MowadiyanSubmissions
            .Include(s => s.InvoiceRecord)
            .AsNoTracking()
            .AsQueryable();

        if (since.HasValue)
        {
            query = query.Where(s => s.UpdatedAt >= since.Value);
        }

        var items = await query
            .OrderByDescending(s => s.CreatedAt)
            .Take(500)
            .Select(s => new
            {
                id = s.Id,
                invoiceNumber = s.InvoiceRecord != null ? s.InvoiceRecord.InvoiceNumber : (string?)null,
                status = s.StatusText,
                taxReferenceId = s.TaxReferenceId,
                fiscalId = s.FiscalId,
                retryCount = s.RetryCount,
                errorMessage = s.ErrorMessage,
                updatedAt = s.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        return Ok(new { success = true, count = items.Count, items });
    }

    /// <summary>
    /// Acknowledges receipt of a synced payload. Intended to be called by the external
    /// ERP after a successful pull so the integration can be tracked.
    /// </summary>
    [HttpPost("acknowledge")]
    [Authorize(AuthenticationSchemes = ApiKeyAuthenticationExtensions.AuthenticationScheme)]
    [Produces("application/json")]
    public IActionResult Acknowledge([FromBody] ErpAckRequest request)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Source))
        {
            return BadRequest(new { success = false, message = "منبع (Source) اجباری است." });
        }

        _logger.LogInformation(
            "External ERP '{Source}' acknowledged {Count} records (batch {BatchId}).",
            request.Source, request.RecordCount, request.BatchId);

        return Ok(new { success = true, message = "دریافت شد.", receivedAt = DateTimeOffset.UtcNow });
    }
}

/// <summary>Request body for the integration acknowledge endpoint.</summary>
public sealed class ErpAckRequest
{
    public string Source { get; set; } = string.Empty;
    public string? BatchId { get; set; }
    public int RecordCount { get; set; }
}