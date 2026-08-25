using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Models;
using OfficeAutomation.Modules.Office.Infrastructure.Persistence;

namespace OfficeAutomation.Services.Contracts;

public sealed class PaymentPenaltyService : IPaymentPenaltyService
{
    private readonly OfficeDbContext _context;
    private readonly ILogger<PaymentPenaltyService> _logger;

    public PaymentPenaltyService(OfficeDbContext context, ILogger<PaymentPenaltyService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<PaymentPenaltyRule> CreateDefaultRuleAsync(int contractId, CancellationToken ct = default)
    {
        var existing = await _context.PaymentPenaltyRules
            .FirstOrDefaultAsync(r => r.ContractId == contractId && r.IsActive, ct);
        if (existing != null) return existing;
        var rule = new PaymentPenaltyRule
        {
            ContractId = contractId, GracePeriodDays = 0,
            PenaltyType = PenaltyTypeNames.PercentagePerMonth, PenaltyPercentage = 2m,
            CapType = CapTypeNames.None, IsActive = true
        };
        _context.PaymentPenaltyRules.Add(rule);
        await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Default penalty rule created for contract {ContractId}", contractId);
        return rule;
    }

    public async Task<PaymentPenaltyRule?> GetActiveRuleAsync(int contractId, CancellationToken ct = default)
        => await _context.PaymentPenaltyRules.AsNoTracking()
            .FirstOrDefaultAsync(r => r.ContractId == contractId && r.IsActive, ct);

    public async Task<PaymentPenaltyRule> SaveRuleAsync(PaymentPenaltyRuleEditVM model, int? ruleId = null, CancellationToken ct = default)
    {
        PaymentPenaltyRule rule;
        if (ruleId.HasValue)
        {
            rule = await _context.PaymentPenaltyRules.FindAsync(new object[] { ruleId.Value }, ct)
                ?? throw new InvalidOperationException("ﬁ«‰Ê‰ Ã—Ì„Â Ì«›  ‰‘œ.");
        }
        else
        {
            rule = new PaymentPenaltyRule { ContractId = model.ContractId };
            _context.PaymentPenaltyRules.Add(rule);
        }
        rule.GracePeriodDays = model.GracePeriodDays;
        rule.PenaltyType = model.PenaltyType;
        rule.PenaltyAmount = model.PenaltyAmount;
        rule.PenaltyPercentage = model.PenaltyPercentage;
        rule.CapType = model.CapType;
        rule.CapValue = model.CapValue;
        rule.IsActive = model.IsActive;
        rule.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);
        return rule;
    }

    public async Task<CalculatedPenaltyVM?> CalculatePenaltyAsync(int paymentId, CancellationToken ct = default)
    {
        var payment = await _context.ContractPayments.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == paymentId, ct);
        if (payment == null) return null;
        if (payment.Status != "Pending" && payment.Status != "Overdue") return null;
        if (!payment.DueDate.HasValue) return null;
        var existing = await _context.CalculatedPenalties.AsNoTracking()
            .FirstOrDefaultAsync(p => p.PaymentId == paymentId, ct);
        if (existing != null) return MapToVM(existing);

        var rule = await GetActiveRuleAsync(payment.ContractId, ct);
        if (rule == null) return null;
        var today = DateTime.Today;
        var dueDate = payment.DueDate.Value.Date;
        var graceEnd = dueDate.AddDays(rule.GracePeriodDays);
        if (today <= graceEnd) return null;

        var overdueDays = (today - graceEnd).Days;
        decimal penalty = 0;
        var basis = new List<string>();
        var amt = payment.Amount;

        switch (rule.PenaltyType)
        {
            case PenaltyTypeNames.Fixed:
                penalty = rule.PenaltyAmount ?? 0;
                basis.Add($"Ã—Ì„Â À«» : {penalty:N0}"); break;
            case PenaltyTypeNames.PercentagePerDay:
                penalty = amt * (rule.PenaltyPercentage ?? 0) / 100m * overdueDays;
                basis.Add($"{amt:N0} ◊ {rule.PenaltyPercentage}% ◊ {overdueDays} —Ê“ = {penalty:N0}"); break;
            case PenaltyTypeNames.PercentagePerWeek:
                var w = Math.Ceiling(overdueDays / 7.0);
                penalty = amt * (rule.PenaltyPercentage ?? 0) / 100m * (decimal)w;
                basis.Add($"{amt:N0} ◊ {rule.PenaltyPercentage}% ◊ {w} Â› Â = {penalty:N0}"); break;
            case PenaltyTypeNames.PercentagePerMonth:
                var m = Math.Ceiling(overdueDays / 30.0);
                penalty = amt * (rule.PenaltyPercentage ?? 0) / 100m * (decimal)m;
                basis.Add($"{amt:N0} ◊ {rule.PenaltyPercentage}% ◊ {m} „«Â = {penalty:N0}"); break;
            case PenaltyTypeNames.Compound:
                var dr = (rule.PenaltyPercentage ?? 0) / 100m / 30m;
                penalty = amt * (decimal)Math.Pow((double)(1 + dr), overdueDays) - amt;
                basis.Add($"„—ò»: {amt:N0} ◊ (1 + {dr:P})^({overdueDays} —Ê“) - {amt:N0} = {penalty:N0}"); break;
        }

        if (penalty > 0 && rule.CapType != CapTypeNames.None && rule.CapValue.HasValue && rule.CapValue.Value > 0)
        {
            var cap = rule.CapType switch
            {
                CapTypeNames.AbsoluteAmount => rule.CapValue.Value,
                CapTypeNames.PercentageOfContract => amt * rule.CapValue.Value / 100m,
                _ => 0
            };
            if (cap > 0 && penalty > cap) { basis.Add($"”ﬁ›: {penalty:N0} ? {cap:N0}"); penalty = cap; }
        }

        penalty = Math.Round(penalty, 2);
        var calc = new CalculatedPenalty
        {
            PaymentId = paymentId, ContractId = payment.ContractId,
            CalculatedAmount = penalty, AppliedDate = today,
            Status = PenaltyStatusNames.Pending,
            CalculationBasis = string.Join(" | ", basis)
        };
        _context.CalculatedPenalties.Add(calc);
        await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Penalty {Amount} for payment {PaymentId}", penalty, paymentId);
        return MapToVM(calc);
    }

    public async Task<int> ApplyCalculatedPenaltiesAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var overdue = await _context.ContractPayments
            .Where(p => p.DueDate.HasValue && p.DueDate.Value.Date < now.Date
                && (p.Status == "Pending" || p.Status == "Overdue"))
            .ToListAsync(ct);
        int count = 0;
        foreach (var p in overdue)
        {
            if (await _context.CalculatedPenalties.AnyAsync(x => x.PaymentId == p.Id && x.AppliedDate == now.Date, ct))
                continue;
            var r = await CalculatePenaltyAsync(p.Id, ct);
            if (r != null) count++;
        }
        foreach (var p in overdue)
            if (p.Status == "Pending") p.Status = "Overdue";
        if (overdue.Count > 0) await _context.SaveChangesAsync(ct);
        _logger.LogInformation("ApplyCalculatedPenalties: {Count} penalties, {Overdue} overdue", count, overdue.Count);
        return count;
    }

    public async Task<List<CalculatedPenaltyVM>> GetPenaltiesForContractAsync(int contractId, CancellationToken ct = default)
        => await _context.CalculatedPenalties.AsNoTracking()
            .Where(p => p.ContractId == contractId)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => MapToVM(p)).ToListAsync(ct);

    public async Task<List<PaymentPenaltyRuleVM>> GetRulesForContractAsync(int contractId, CancellationToken ct = default)
        => await _context.PaymentPenaltyRules.AsNoTracking()
            .Where(r => r.ContractId == contractId)
            .OrderByDescending(r => r.IsActive).ThenByDescending(r => r.CreatedAt)
            .Select(r => new PaymentPenaltyRuleVM
            {
                Id = r.Id, ContractId = r.ContractId, GracePeriodDays = r.GracePeriodDays,
                PenaltyType = r.PenaltyType, PenaltyAmount = r.PenaltyAmount,
                PenaltyPercentage = r.PenaltyPercentage, CapType = r.CapType,
                CapValue = r.CapValue, IsActive = r.IsActive
            }).ToListAsync(ct);

    public async Task<bool> UpdatePenaltyStatusAsync(int penaltyId, string newStatus, CancellationToken ct = default)
    {
        var p = await _context.CalculatedPenalties.FindAsync(new object[] { penaltyId }, ct);
        if (p == null) return false;
        if (!PenaltyStatusNames.All.Contains(newStatus))
            throw new InvalidOperationException("Ê÷⁄Ì  Ã—Ì„Â ‰«„⁄ »— «” .");
        p.Status = newStatus;
        await _context.SaveChangesAsync(ct);
        return true;
    }

    private static CalculatedPenaltyVM MapToVM(CalculatedPenalty p) => new()
    {
        Id = p.Id, PaymentId = p.PaymentId, ContractId = p.ContractId,
        CalculatedAmount = p.CalculatedAmount, AppliedDate = p.AppliedDate,
        Status = p.Status, CalculationBasis = p.CalculationBasis, CreatedAt = p.CreatedAt
    };
}
