using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OfficeAutomation.Models;
using OfficeAutomation.Modules.Office.Infrastructure.Persistence;

namespace OfficeAutomation.Services.Contracts;

/// <summary>
/// پياده‌سازی سرويس برنامه زمان‌بندی پرداخت قراردادها.
/// پشتيبانی از انواع: Fixed, Milestone, Percentage, Custom.
/// </summary>
public sealed class PaymentScheduleService : IPaymentScheduleService
{
    private readonly OfficeDbContext _context;
    private readonly ILogger<PaymentScheduleService> _logger;

    public PaymentScheduleService(OfficeDbContext context, ILogger<PaymentScheduleService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ContractPaymentSchedule> GenerateScheduleAsync(
        PaymentScheduleVM model, string currentUserId, CancellationToken ct = default)
    {
        var contract = await _context.Contracts.FirstOrDefaultAsync(c => c.Id == model.ContractId, ct)
            ?? throw new KeyNotFoundException("Contract " + model.ContractId + " not found.");

        var existing = await _context.Set<ContractPaymentSchedule>()
            .Include(s => s.Installments)
            .FirstOrDefaultAsync(s => s.ContractId == model.ContractId, ct);
        if (existing != null) _context.Set<ContractPaymentSchedule>().Remove(existing);

        var schedule = new ContractPaymentSchedule
        {
            ContractId = model.ContractId, ScheduleType = model.ScheduleType,
            TotalAmount = contract.TotalAmount, InstallmentCount = model.InstallmentCount,
            CreatedAt = DateTime.UtcNow, CreatedByUserId = currentUserId,
        };

        schedule.Installments = GenerateInstallments(model.ScheduleType, contract.TotalAmount,
            model.InstallmentCount, model.StartDate, model.IntervalDays);

        _context.Set<ContractPaymentSchedule>().Add(schedule);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Payment schedule created for contract {0}. Type={1}, Count={2}",
            model.ContractId, model.ScheduleType, model.InstallmentCount);
        return schedule;
    }

    public async Task<PaymentScheduleVM?> GetScheduleAsync(int contractId, CancellationToken ct = default)
    {
        var schedule = await _context.Set<ContractPaymentSchedule>()
            .AsNoTracking().Include(s => s.Installments)
            .FirstOrDefaultAsync(s => s.ContractId == contractId, ct);
        if (schedule == null) return null;

        var contract = await _context.Contracts.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == contractId, ct);

        decimal cumulative = 0;
        var installments = schedule.Installments.OrderBy(i => i.InstallmentNumber).Select(i =>
        {
            cumulative += i.Amount;
            return new PaymentScheduleInstallmentVM
            {
                Id = i.Id, InstallmentNumber = i.InstallmentNumber,
                DueDate = i.DueDate, Amount = i.Amount, Percentage = i.Percentage,
                Description = i.Description, Status = i.Status,
                PaidAmount = i.PaidAmount, PaymentId = i.PaymentId,
                CumulativeAmount = cumulative,
            };
        }).ToList();

        return new PaymentScheduleVM
        {
            ContractId = contractId,
            ScheduleId = schedule.Id, ContractNumber = contract?.ContractNumber ?? "",
            ContractTotalAmount = contract?.TotalAmount ?? 0,
            ScheduleType = schedule.ScheduleType, InstallmentCount = schedule.InstallmentCount,
            StartDate = schedule.Installments.Count > 0 ? schedule.Installments.Min(i => i.DueDate) : DateTime.Today,
            IntervalDays = 30,
            Installments = installments,
        };
    }

    public async Task<bool> DeleteScheduleAsync(int scheduleId, string currentUserId, CancellationToken ct = default)
    {
        var s = await _context.Set<ContractPaymentSchedule>()
            .Include(s => s.Installments).FirstOrDefaultAsync(s => s.Id == scheduleId, ct);
        if (s == null) return false;
        _context.Set<ContractPaymentSchedule>().Remove(s);
        await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Payment schedule {0} deleted by user {1}.", scheduleId, currentUserId);
        return true;
    }

    public async Task UpdateOverdueStatusAsync(CancellationToken ct = default)
    {
        var today = DateTime.Today;
        var overdue = await _context.Set<ContractPaymentScheduleInstallment>()
            .Where(i => i.Status == "Pending" && i.DueDate < today).ToListAsync(ct);
        foreach (var i in overdue) { i.Status = "Overdue"; }
        if (overdue.Count > 0) { await _context.SaveChangesAsync(ct); _logger.LogInformation("{0} installments marked Overdue.", overdue.Count); }
    }

    public async Task<ContractPaymentSchedule> CustomizeInstallmentsAsync(
        int scheduleId, List<PaymentScheduleInstallmentVM> customInstallments,
        string currentUserId, CancellationToken ct = default)
    {
        var schedule = await _context.Set<ContractPaymentSchedule>()
            .Include(s => s.Installments)
            .FirstOrDefaultAsync(s => s.Id == scheduleId, ct)
            ?? throw new KeyNotFoundException("Schedule " + scheduleId + " not found.");

        var updatedTotal = customInstallments.Sum(i => i.Amount);
        if (updatedTotal != schedule.TotalAmount)
            throw new ArgumentException(
                $"مجموع مبالغ اقساط ({updatedTotal:N0}) با مبلغ کل قرارداد ({schedule.TotalAmount:N0}) مطابقت ندارد.");

        _context.Set<ContractPaymentScheduleInstallment>().RemoveRange(schedule.Installments);
        schedule.Installments.Clear();

        int seq = 0;
        foreach (var ci in customInstallments)
        {
            seq++;
            schedule.Installments.Add(new ContractPaymentScheduleInstallment
            {
                InstallmentNumber = seq,
                DueDate = ci.DueDate,
                Amount = ci.Amount,
                Percentage = null,
                Description = ci.Description ?? "قسط " + seq,
                Status = "Pending",
            });
        }

        schedule.InstallmentCount = seq;
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Installments customized for schedule {0}: {1} installments, total={2}.",
            schedule.Id, seq, updatedTotal);
        return schedule;
    }

    private static List<ContractPaymentScheduleInstallment> GenerateInstallments(
        string type, decimal total, int count, DateTime startDate, int intervalDays)
    {
        var list = new List<ContractPaymentScheduleInstallment>();
        switch (type)
        {
            case "Fixed":
                var baseAmt = Math.Round(total / count, 2);
                var rem = total - (baseAmt * (count - 1));
                for (int i = 1; i <= count; i++)
                    list.Add(new ContractPaymentScheduleInstallment
                    {
                        InstallmentNumber = i, DueDate = startDate.AddDays((i - 1) * intervalDays),
                        Amount = i == count ? rem : baseAmt, Status = "Pending",
                        Description = "قسط " + i + " از " + count,
                    });
                break;
            case "Percentage":
                var pct = Math.Round(100m / count, 2);
                var pctRem = 100m - (pct * (count - 1));
                for (int i = 1; i <= count; i++)
                {
                    var p = i == count ? pctRem : pct;
                    list.Add(new ContractPaymentScheduleInstallment
                    {
                        InstallmentNumber = i, DueDate = startDate.AddDays((i - 1) * intervalDays),
                        Amount = Math.Round(total * p / 100m, 2), Percentage = p, Status = "Pending",
                        Description = "درصد " + p + "%",
                    });
                }
                break;
            case "Milestone":
                var msBase = Math.Round(total / count, 2);
                var msRem = total - (msBase * (count - 1));
                var labels = new[] { "پيش‌پرداخت", "مرحله اول", "مرحله دوم", "مرحله سوم", "مرحله چهارم", "مرحله پنجم", "مرحله ششم", "مرحله هفتم", "مرحله هشتم", "مرحله نهم", "مرحله دهم", "تحويل نهايی" };
                for (int i = 1; i <= count; i++)
                    list.Add(new ContractPaymentScheduleInstallment
                    {
                        InstallmentNumber = i, DueDate = startDate.AddDays((i - 1) * intervalDays),
                        Amount = i == count ? msRem : msBase, Status = "Pending",
                        Description = (i - 1) < labels.Length ? labels[i - 1] : "مرحله " + i,
                    });
                break;
            case "Custom":
                var cBase = Math.Round(total / count, 2);
                var cRem = total - (cBase * (count - 1));
                for (int i = 1; i <= count; i++)
                    list.Add(new ContractPaymentScheduleInstallment
                    {
                        InstallmentNumber = i, DueDate = startDate.AddDays((i - 1) * intervalDays),
                        Amount = i == count ? cRem : cBase, Status = "Pending",
                        Description = "قسط " + i,
                    });
                break;
            default: throw new ArgumentException("Unsupported type: " + type);
        }
        return list;
    }
}

