using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OfficeAutomation.Modules.Persistence;
using OfficeAutomation.Modules.Persistence.Entities;

namespace OfficeAutomation.Modules.MowadiyanIntegration;

/// <summary>
/// Background hosted service that consumes the Mowadiyan submission channel and
/// uploads invoices to the Tax Portal with automatic retry and exponential backoff.
/// </summary>
public sealed class MowadiyanSubmissionQueueHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMowadiyanSubmissionService _submissionService;
    private readonly IOptions<MowadiyanOptions> _options;
    private readonly ILogger<MowadiyanSubmissionQueueHostedService> _logger;

    public MowadiyanSubmissionQueueHostedService(
        IServiceScopeFactory scopeFactory,
        IMowadiyanSubmissionService submissionService,
        IOptions<MowadiyanOptions> options,
        ILogger<MowadiyanSubmissionQueueHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _submissionService = submissionService;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Mowadiyan submission queue hosted service started.");

        // On startup, enqueue any leftover pending submissions.
        try
        {
            await _submissionService.EnqueuePendingAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enqueue pending Mowadiyan submissions on startup.");
        }

        await foreach (var invoiceRecordId in _submissionService.Reader.ReadAllAsync(stoppingToken))
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var delay = await ProcessInvoiceAsync(invoiceRecordId, stoppingToken);
                if (delay <= TimeSpan.Zero)
                {
                    break;
                }

}
        }

        _logger.LogInformation("Mowadiyan submission queue hosted service stopped.");
    }

    /// <summary>
    /// Processes one invoice. Returns the backoff delay to wait before retrying,
    /// or <see cref="TimeSpan.Zero"/> when the invoice completed (or retries exhausted).
    /// </summary>
    private async Task<TimeSpan> ProcessInvoiceAsync(Guid invoiceRecordId, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IDbContextFactory<TaxDbContext>>();
        var payloadBuilder = scope.ServiceProvider.GetRequiredService<IMowadiyanPayloadBuilder>();
        var crypto = scope.ServiceProvider.GetRequiredService<IMowadiyanCryptoService>();

        // Select the sandbox client when configured (offline development), otherwise the real portal client.
        IMowadiyanApiClient apiClient = _options.Value.IsSandbox
            ? scope.ServiceProvider.GetRequiredService<SandboxMowadiyanApiClient>()
            : scope.ServiceProvider.GetRequiredService<IMowadiyanApiClient>();

        using var ctx = await db.CreateDbContextAsync(cancellationToken);

        var submission = await ctx.MowadiyanSubmissions
            .Where(s => s.InvoiceRecordId == invoiceRecordId)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (submission is null)
        {
            // No submission record yet — create a Draft entry for the invoice.
            submission = new MowadiyanSubmission
            {
                InvoiceRecordId = invoiceRecordId,
                Status = MowadiyanSubmissionStatus.Draft,
                StatusText = "Draft"
            };
            ctx.MowadiyanSubmissions.Add(submission);
            await ctx.SaveChangesAsync(cancellationToken);
        }

        if (submission.Status == MowadiyanSubmissionStatus.Success)
        {
            return TimeSpan.Zero;
        }

        var options = _options.Value;
        if (submission.RetryCount >= options.MaxRetryCount)
        {
            await MarkStatusAsync(ctx, submission, MowadiyanSubmissionStatus.RetryLimitExceeded, "حداکثر تلاش برای ارسال انجام شد.");
            return TimeSpan.Zero;
        }

        var invoice = await ctx.InvoiceRecords
            .Include(r => r.Seller)
            .Include(r => r.Buyer)
            .Include(r => r.LineItems)
            .FirstOrDefaultAsync(r => r.Id == invoiceRecordId, cancellationToken);

        if (invoice is null)
        {
            _logger.LogWarning("Invoice {InvoiceId} not found for Mowadiyan submission.", invoiceRecordId);
            return TimeSpan.Zero;
        }

        var settings = await ResolveSettingsAsync(ctx, options, cancellationToken);
        if (settings is null)
        {
            await MarkStatusAsync(ctx, submission, MowadiyanSubmissionStatus.RejectedByTaxOrg,
                "تنظیمات سامانه مودیان پیکربندی نشده است.");
            return TimeSpan.Zero;
        }

        await MarkStatusAsync(ctx, submission, MowadiyanSubmissionStatus.PendingSubmission, "PendingSubmission");

        try
        {
            var payload = payloadBuilder.Build(invoice, settings);
            var payloadJson = payloadBuilder.Serialize(payload);
            submission.PayloadJson = payloadJson;

            string? envelope = null;
            try
            {
                envelope = crypto.CreatePkcs7Envelope(System.Text.Encoding.UTF8.GetBytes(payloadJson), settings);
                submission.SignedEnvelope = envelope;
            }
            catch (Exception signEx)
            {
                _logger.LogWarning(signEx, "PKCS#7 envelope skipped (no valid private key). Sending normal payload.");
            }

            await ctx.SaveChangesAsync(cancellationToken);

            var result = await apiClient.SubmitInvoiceAsync(settings, payloadJson, envelope, cancellationToken);

            submission.Status = MowadiyanSubmissionStatus.Submitted;
            submission.StatusText = "Submitted";
            submission.TaxReferenceId = result.TaxReferenceId;
            submission.FiscalId = result.FiscalId;
            submission.ResponseJson = result.ResponseJson;
            submission.SubmittedAt = DateTime.Now;
            await ctx.SaveChangesAsync(cancellationToken);

            if (result.Success)
            {
                submission.Status = MowadiyanSubmissionStatus.Success;
                submission.StatusText = "Success";
                submission.CompletedAt = DateTime.Now;
                await ctx.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Mowadiyan invoice {Id} submitted successfully. Ref={Ref}",
                    invoiceRecordId, result.TaxReferenceId);
                return TimeSpan.Zero;
            }

            // Failure — schedule retry with exponential backoff.
            submission.RetryCount++;
            submission.ErrorMessage = result.ErrorMessage;
            submission.Status = MowadiyanSubmissionStatus.RejectedByTaxOrg;
            submission.StatusText = "RejectedByTaxOrg";
            submission.NextRetryAtUtc = DateTime.UtcNow.AddSeconds(ComputeBackoff(submission.RetryCount, options));
            await ctx.SaveChangesAsync(cancellationToken);

            _logger.LogWarning("Mowadiyan rejection for invoice {Id}: {Error}", invoiceRecordId, result.ErrorMessage);
            return TimeSpan.FromSeconds(ComputeBackoff(submission.RetryCount, options));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting invoice {InvoiceId} to Mowadiyan.", invoiceRecordId);

            submission.RetryCount++;
            submission.ErrorMessage = ex.Message;
            submission.Status = MowadiyanSubmissionStatus.RejectedByTaxOrg;
            submission.StatusText = "RejectedByTaxOrg";
            submission.NextRetryAtUtc = DateTime.UtcNow.AddSeconds(ComputeBackoff(submission.RetryCount, options));
            await ctx.SaveChangesAsync(cancellationToken);

            return TimeSpan.FromSeconds(ComputeBackoff(submission.RetryCount, options));
        }
    }

    private async Task<MowadiyanSettings?> ResolveSettingsAsync(
        TaxDbContext ctx, MowadiyanOptions options, CancellationToken cancellationToken)
    {
        var settings = await ctx.MowadiyanSettings
            .OrderByDescending(s => s.UpdatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (settings is null)
        {
            return null;
        }

        // Merge runtime options into the persisted settings as fallbacks.
        if (string.IsNullOrWhiteSpace(settings.ClientId))
        {
            settings.ClientId = options.ClientId;
        }
        if (string.IsNullOrWhiteSpace(settings.EconomicCode))
        {
            settings.EconomicCode = options.EconomicCode;
        }
        if (string.IsNullOrWhiteSpace(settings.BaseUrl))
        {
            settings.BaseUrl = options.BaseUrl;
        }
        settings.IsSandbox = options.IsSandbox || settings.IsSandbox;
        return settings;
    }

    private static async Task MarkStatusAsync(
        TaxDbContext ctx,
        MowadiyanSubmission submission,
        MowadiyanSubmissionStatus status,
        string statusText)
    {
        submission.Status = status;
        submission.StatusText = statusText;
        submission.UpdatedAt = DateTime.Now;
        await ctx.SaveChangesAsync();
    }

    private int ComputeBackoff(int retryCount, MowadiyanOptions options)
    {
        // Exponential backoff: base 2^retry * initial, capped at max.
        var seconds = options.InitialBackoffSeconds * Math.Pow(2, retryCount - 1);
        return (int)Math.Min(seconds, options.MaxBackoffSeconds);
    }
}