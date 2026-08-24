using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Models;
using OfficeAutomation.Modules.Office.Infrastructure.Persistence;
using OfficeAutomation.Services.Tenancy;

namespace OfficeAutomation.Services.Contracts;

public sealed class AlertService : IAlertService
{
    private readonly OfficeDbContext _context;
    private readonly ICurrentTenantAccessor? _tenantAccessor;

    public AlertService(OfficeDbContext context, ICurrentTenantAccessor? tenantAccessor = null)
    {
        _context = context;
        _tenantAccessor = tenantAccessor;
    }

    public async Task<List<AlertRule>> GetRulesAsync(string? sourceModule = null, CancellationToken ct = default)
    {
        var query = _context.Set<AlertRule>().AsQueryable();
        if (!string.IsNullOrWhiteSpace(sourceModule))
        {
            query = query.Where(r => r.SourceModule == sourceModule);
        }
        return await query.OrderBy(r => r.DisplayName).ToListAsync(ct);
    }

    public async Task<AlertSettingsVM> GetUserAlertSettingsAsync(string userId, string? sourceModule = null, CancellationToken ct = default)
    {
        var rulesQuery = _context.Set<AlertRule>().AsQueryable();
        if (!string.IsNullOrWhiteSpace(sourceModule))
        {
            rulesQuery = rulesQuery.Where(r => r.SourceModule == sourceModule);
        }
        var rules = await rulesQuery.OrderBy(r => r.DisplayName).ToListAsync(ct);

        var subscriptions = await _context.Set<AlertSubscription>()
            .Where(s => s.UserId == userId)
            .ToListAsync(ct);

        var subDict = subscriptions.ToDictionary(s => s.AlertRuleId);

        var ruleVms = rules.Select(r =>
        {
            var hasSub = subDict.TryGetValue(r.Id, out var sub);
            return new AlertRuleVM
            {
                Id = r.Id,
                TriggerType = r.TriggerType,
                DisplayName = r.DisplayName,
                Description = r.Description,
                Severity = r.Severity,
                IsActive = r.IsActive,
                IsSystemRule = r.IsSystemRule,
                SendInApp = r.SendInApp,
                SendEmail = r.SendEmail,
                AdvanceNoticeDays = r.AdvanceNoticeDays,
                SubscribedInApp = hasSub ? sub!.ReceiveInApp : r.SendInApp,
                SubscribedEmail = hasSub ? sub!.ReceiveEmail : r.SendEmail
            };
        }).ToList();

        return new AlertSettingsVM { Rules = ruleVms };
    }

    public async Task UpdateSubscriptionAsync(string userId, int ruleId, bool receiveInApp, bool receiveEmail, CancellationToken ct = default)
    {
        var existing = await _context.Set<AlertSubscription>()
            .FirstOrDefaultAsync(s => s.UserId == userId && s.AlertRuleId == ruleId, ct);

        if (existing == null)
        {
            existing = new AlertSubscription
            {
                UserId = userId,
                AlertRuleId = ruleId,
                TenantId = _tenantAccessor?.TenantId
            };
            _context.Set<AlertSubscription>().Add(existing);
        }

        existing.ReceiveInApp = receiveInApp;
        existing.ReceiveEmail = receiveEmail;
        existing.UpdatedAt = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync(ct);
    }

    public async Task SeedSystemRulesAsync(CancellationToken ct = default)
    {
        foreach (var (triggerType, displayName, severity, description) in AlertTriggerTypes.Defaults)
        {
            var exists = await _context.Set<AlertRule>()
                .AnyAsync(r => r.TriggerType == triggerType, ct);

            if (!exists)
            {
                _context.Set<AlertRule>().Add(new AlertRule
                {
                    TenantId = _tenantAccessor?.TenantId,
                    SourceModule = "Contracts",
                    TriggerType = triggerType,
                    DisplayName = displayName,
                    Description = description,
                    Severity = severity,
                    IsSystemRule = true,
                    IsActive = triggerType == AlertTriggerTypes.ContractGuaranteeExpiry ? false : true,
                    SendInApp = true,
                    SendEmail = false,
                    AdvanceNoticeDays = triggerType switch
                    {
                        AlertTriggerTypes.ContractExpiring90 => 90,
                        AlertTriggerTypes.ContractExpiring60 => 60,
                        AlertTriggerTypes.ContractExpiring30 => 30,
                        AlertTriggerTypes.ContractExpiring14 => 14,
                        AlertTriggerTypes.ContractExpiring7  => 7,
                        AlertTriggerTypes.ContractExpiring1  => 1,
                        _ => null
                    }
                });
            }
        }

        await _context.SaveChangesAsync(ct);
    }

    public async Task<bool> IsSubscribedInAppAsync(string userId, string triggerType, CancellationToken ct = default)
    {
        var rule = await _context.Set<AlertRule>()
            .FirstOrDefaultAsync(r => r.TriggerType == triggerType && r.IsActive, ct);

        if (rule == null) return true; // default: allow

        var sub = await _context.Set<AlertSubscription>()
            .FirstOrDefaultAsync(s => s.UserId == userId && s.AlertRuleId == rule.Id, ct);

        if (sub == null) return rule.SendInApp;

        return sub.ReceiveInApp;
    }

    public async Task<bool> IsSubscribedEmailAsync(string userId, string triggerType, CancellationToken ct = default)
    {
        var rule = await _context.Set<AlertRule>()
            .FirstOrDefaultAsync(r => r.TriggerType == triggerType && r.IsActive, ct);

        if (rule == null) return false;

        var sub = await _context.Set<AlertSubscription>()
            .FirstOrDefaultAsync(s => s.UserId == userId && s.AlertRuleId == rule.Id, ct);

        if (sub == null) return rule.SendEmail;

        return sub.ReceiveEmail;
    }
}