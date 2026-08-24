using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OfficeAutomation.Services.Contracts;

namespace OfficeAutomation.Services;

public sealed class ContractBackgroundJobCoordinator : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ContractBackgroundJobCoordinator> _logger;

    public ContractBackgroundJobCoordinator(
        IServiceScopeFactory scopeFactory,
        ILogger<ContractBackgroundJobCoordinator> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = DateTime.UtcNow;
                if (now.Hour == 1)
                {
                    using var scope = _scopeFactory.CreateScope();
                    await RunExpiryCheckAsync(scope.ServiceProvider, now, stoppingToken);
                    await RunOverduePaymentCheckAsync(scope.ServiceProvider, now, stoppingToken);
                }
                if (now.Hour == 2)
                {
                    using var scope = _scopeFactory.CreateScope();
                    await RunRevenueRecognitionAsync(scope.ServiceProvider, now, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { _logger.LogError(ex, "Error in coordinator."); }
            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }

    private async Task RunExpiryCheckAsync(IServiceProvider services, DateTime now, CancellationToken ct)
    {
        var ctx = services.GetRequiredService<Modules.Office.Infrastructure.Persistence.OfficeDbContext>();
        var ns = services.GetRequiredService<NotificationService>();
        var alert = services.GetRequiredService<IAlertService>();
        var today = now.Date;

        var toExpire = await ctx.Contracts
            .Where(c => c.EndDate != null && c.EndDate.Value.Date < today
                && (c.Status == ContractStatuses.Active || c.Status == ContractStatuses.ExpiringSoon))
            .ToListAsync(ct);

        foreach (var c in toExpire)
        {
            c.Status = ContractStatuses.Expired;
            await ns.CreateAsync(
                recipientUserId: c.AssignedUserId ?? c.CreatedByUserId,
                title: "انقضای قرارداد",
                message: "قرارداد \u00ab" + c.Title + "\u00bb (شماره " + c.ContractNumber + ") منقضی شده است.",
                severity: NotificationSeverity.Warning,
                linkUrl: "/Contracts/Details/" + c.Id,
                sourceModule: "Contracts", sourceEntityType: "Contract", sourceEntityId: c.Id,
                cancellationToken: ct);
        }

        // Multi-tier expiry alerts (90/60/30/14/7/1 days)
        // Range-based: EndDate within [today+lower, today+tier] so missed runs are tolerated.
        // UpsertActiveAsync dedup prevents duplicate sends on subsequent runs.
        var tiers = new[]
        {
            (90, 61, AlertTriggerTypes.ContractExpiring90,   "انقضای نزدیک - 90 روز",  "در 90 روز آینده منقضی می‌شود.",  NotificationSeverity.Info),
            (60, 31, AlertTriggerTypes.ContractExpiring60,   "انقضای نزدیک - 60 روز",  "در 60 روز آینده منقضی می‌شود.",  NotificationSeverity.Info),
            (30, 15, AlertTriggerTypes.ContractExpiring30,   "انقضای نزدیک - 30 روز",  "در 30 روز آینده منقضی می‌شود.",  NotificationSeverity.Warning),
            (14, 8,  AlertTriggerTypes.ContractExpiring14,   "انقضای نزدیک - 14 روز",  "در 14 روز آینده منقضی می‌شود.",  NotificationSeverity.Warning),
            (7,  2,  AlertTriggerTypes.ContractExpiring7,    "انقضای نزدیک - 7 روز",   "در 7 روز آینده منقضی می‌شود.",   NotificationSeverity.Danger),
            (1,  1,  AlertTriggerTypes.ContractExpiring1,    "انقضای نزدیک - فردا",     "فردا منقضی می‌شود.",                    NotificationSeverity.Danger),
        };

        foreach (var (d, lower, typ, ttl, sfx, sev) in tiers)
        {
            var tc = await ctx.Contracts
                .Where(c => c.EndDate != null
                    && c.EndDate.Value.Date >= today.AddDays(lower)
                    && c.EndDate.Value.Date <= today.AddDays(d)
                    && (c.Status == ContractStatuses.Active || c.Status == ContractStatuses.ExpiringSoon)
                    && !c.IsArchived)
                .ToListAsync(ct);
            foreach (var c in tc)
            {
                var rid = c.AssignedUserId ?? c.CreatedByUserId;
                if (string.IsNullOrEmpty(rid)) continue;
                if (await alert.IsSubscribedInAppAsync(rid, typ, ct))
                {
                    await ns.UpsertActiveAsync(rid, ttl,
                        $"\u0642\u0631\u0627\u0631\u062f\u0627\u062f \u00ab{c.Title}\u00bb (\u0634\u0645\u0627\u0631\u0647 {c.ContractNumber}) {sfx}",
                        sev, $"/Contracts/Details/{c.Id}",
                        "Contracts", "Contract", c.Id, c.EndDate?.AddDays(1), ct);
                }
            }
        }

        foreach (var c in toExpire)
        {
            var rid = c.AssignedUserId ?? c.CreatedByUserId;
            if (string.IsNullOrEmpty(rid)) continue;
            if (await alert.IsSubscribedInAppAsync(rid, AlertTriggerTypes.ContractExpired, ct))
            {
                await ns.UpsertActiveAsync(rid, "انقضای قرارداد",
                    $"\u0642\u0631\u0627\u0631\u062F\u0627\u062F \u00ab{c.Title}\u00bb (\u0634\u0645\u0627\u0631\u0647 {c.ContractNumber}) \u0645\u0646\u0642\u0636\u06CC \u0634\u062F\u0647 \u0627\u0633\u062A.",
                    NotificationSeverity.Danger, $"/Contracts/Details/{c.Id}",
                    "Contracts", "Contract", c.Id, DateTimeOffset.UtcNow.AddDays(30), ct);
            }
        }

        var renew = await ctx.Contracts
            .Where(c => c.EndDate != null && c.EndDate.Value.Date >= today && c.EndDate.Value.Date <= today.AddDays(14)
                && c.Status == ContractStatuses.Active && !c.IsArchived)
            .ToListAsync(ct);

        foreach (var c in renew)
        {
            var rid = c.AssignedUserId ?? c.CreatedByUserId;
            if (string.IsNullOrEmpty(rid)) continue;
            if (await alert.IsSubscribedInAppAsync(rid, AlertTriggerTypes.ContractRenewalDecision, ct))
            {
                await ns.UpsertActiveAsync(rid, "تصمیم تمدید قرارداد",
                    $"\u0642\u0631\u0627\u0631\u062F\u0627\u062F \u00ab{c.Title}\u00bb (\u0634\u0645\u0627\u0631\u0647 {c.ContractNumber}) \u062f\u0631 {c.EndDate:yyyy/MM/dd} \u0645\u0646\u0642\u0636\u06CC \u0645\u06CC\u200C\u0634\u0648\u062F. \u0644\u0637\u0641\u0627 \u062A\u0635\u0645\u06CC\u0645 \u062A\u0645\u062F\u06CC\u062F \u0631\u0627 \u062B\u0628\u062A \u06A9\u0646\u06CC\u062F.",
                    NotificationSeverity.Warning, $"/Contracts/Details/{c.Id}",
                    "Contracts", "Contract", c.Id, c.EndDate?.AddDays(1), ct);
            }
        }

        // Guarantee/Retention stub -- placeholder for Item 22.
        // ContractGuarantee entity not yet implemented. Rule pre-seeded inactive.
        _ = alert;

        var es = await ctx.Contracts
            .Where(c => c.EndDate != null && c.EndDate.Value.Date >= today && c.EndDate.Value.Date <= today.AddDays(30)
                && c.Status == ContractStatuses.Active && !c.IsArchived)
            .ToListAsync(ct);

        foreach (var c in es) { c.Status = ContractStatuses.ExpiringSoon; }

        if (toExpire.Count > 0 || es.Count > 0)
        {
            await ctx.SaveChangesAsync(ct);
        }
    }

    private async Task RunOverduePaymentCheckAsync(IServiceProvider services, DateTime now, CancellationToken ct)
    {
        var ctx = services.GetRequiredService<Modules.Office.Infrastructure.Persistence.OfficeDbContext>();
        var ns = services.GetRequiredService<NotificationService>();
        var alert = services.GetRequiredService<IAlertService>();
        var today = now.Date;

        var scheduleService = services.GetRequiredService<IPaymentScheduleService>();
        await scheduleService.UpdateOverdueStatusAsync(ct);

        var overdue = await ctx.Set<Models.ContractPaymentScheduleInstallment>()
            .Include(i => i.Schedule).ThenInclude(s => s!.Contract)
            .Where(i => i.Status == "Overdue" && i.DueDate < today)
            .ToListAsync(ct);

        foreach (var inst in overdue)
        {
            var contract = inst.Schedule?.Contract;
            if (contract == null) continue;
            var rid = contract.AssignedUserId ?? contract.CreatedByUserId;
            if (string.IsNullOrEmpty(rid)) continue;

            await ns.CreateAsync(
                recipientUserId: rid,
                title: "قسط سررسید گذشته",
                message: "قسط " + inst.InstallmentNumber + " از قرارداد \u00ab"
                    + contract.Title + "\u00bb سررسید آن گذشته است.",
                severity: NotificationSeverity.Warning,
                linkUrl: "/Contracts/Details/" + contract.Id,
                sourceModule: "Contracts", sourceEntityType: "Contract", sourceEntityId: contract.Id,
                cancellationToken: ct);

            if (await alert.IsSubscribedInAppAsync(rid, AlertTriggerTypes.ContractOverduePayment, ct))
            {
                await ns.UpsertActiveAsync(rid, "قسط معوق",
                    $"\u0642\u0633\u0637 {inst.InstallmentNumber} \u0627\u0632 \u0642\u0631\u0627\u0631\u062F\u0627\u062F \u00ab{contract.Title}\u00bb (\u0634\u0645\u0627\u0631\u0647 {contract.ContractNumber}) \u0633\u0631\u0631\u0633\u06CC\u062F \u06AF\u0630\u0634\u062A\u0647 \u0627\u0633\u062A. \u0645\u0628\u0644\u063A: {inst.Amount:N0}",
                    NotificationSeverity.Danger, $"/Contracts/Details/{contract.Id}",
                    "Contracts", "Contract", contract.Id, DateTimeOffset.UtcNow.AddDays(7), ct);
            }
        }

        var contracts = await ctx.Contracts
            .Where(c => c.Status == Models.ContractStatuses.Active
                || c.Status == Models.ContractStatuses.ExpiringSoon
                || c.Status == Models.ContractStatuses.Expired)
            .ToListAsync(ct);

        int sc = 0;
        foreach (var c in contracts)
        {
            var ns2 = c.TotalAmount <= 0 ? Models.PaymentStatuses.Unpaid
                : c.PaidAmount <= 0 ? Models.PaymentStatuses.Unpaid
                : c.PaidAmount >= c.TotalAmount ? Models.PaymentStatuses.FullyPaid
                : Models.PaymentStatuses.PartiallyPaid;
            if (c.PaymentStatus != ns2) { c.PaymentStatus = ns2; sc++; }
        }
        if (sc > 0) { await ctx.SaveChangesAsync(ct); _logger.LogInformation("{0} payment statuses updated.", sc); }
    }

    private async Task RunRevenueRecognitionAsync(IServiceProvider services, DateTime now, CancellationToken ct)
    {
        var accountingService = services.GetRequiredService<IContractAccountingService>();
        var updated = await accountingService.RecognizeAllActiveContractsAsync(now, "SYSTEM", ct);
        if (updated > 0)
            _logger.LogInformation("Revenue recognition: {0} contracts processed.", updated);
    }
}