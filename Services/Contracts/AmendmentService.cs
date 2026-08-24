using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Models;
using OfficeAutomation.Modules.Office.Infrastructure.Persistence;

namespace OfficeAutomation.Services.Contracts;

public sealed class AmendmentService : IAmendmentService
{
    private readonly OfficeDbContext _context;
    private readonly IApprovalService _approvalService;
    private readonly IVersionService _versionService;
    private readonly IPaymentScheduleService _paymentScheduleService;
    private readonly ILogger<AmendmentService> _logger;

    public AmendmentService(
        OfficeDbContext context,
        IApprovalService approvalService,
        IVersionService versionService,
        IPaymentScheduleService paymentScheduleService,
        ILogger<AmendmentService> logger)
    {
        _context = context;
        _approvalService = approvalService;
        _versionService = versionService;
        _paymentScheduleService = paymentScheduleService;
        _logger = logger;
    }

    public async Task<List<AmendmentListItemVM>> GetAmendmentsForContractAsync(
        int contractId, CancellationToken ct = default)
    {
        return await _context.Set<ContractAmendment>()
            .Where(a => a.ContractId == contractId)
            .OrderByDescending(a => a.AmendmentNumber)
            .Select(a => new AmendmentListItemVM
            {
                Id = a.Id,
                AmendmentType = a.AmendmentType,
                AmendmentNumber = a.AmendmentNumber,
                Title = a.Title,
                Description = a.Description,
                AmountChange = a.AmountChange,
                NewTotalAmount = a.NewTotalAmount,
                EndDateChangeDays = a.EndDateChangeDays,
                NewEndDate = a.NewEndDate,
                EffectiveDate = a.EffectiveDate,
                Status = a.Status,
                CreatedByUserName = a.CreatedByUser != null ? (a.CreatedByUser.FullName ?? a.CreatedByUser.UserName ?? "") : "",
                CreatedAt = a.CreatedAt,
                CreatedAtText = ToPersianDate(a.CreatedAt)
            })
            .ToListAsync(ct);
    }

    public async Task<AmendmentDetailVM?> GetAmendmentDetailAsync(
        int amendmentId, CancellationToken ct = default)
    {
        return await _context.Set<ContractAmendment>()
            .Where(a => a.Id == amendmentId)
            .Select(a => new AmendmentDetailVM
            {
                Id = a.Id,
                ContractId = a.ContractId,
                AmendmentType = a.AmendmentType,
                AmendmentNumber = a.AmendmentNumber,
                Title = a.Title,
                Description = a.Description,
                AmountChange = a.AmountChange,
                NewTotalAmount = a.NewTotalAmount,
                EndDateChangeDays = a.EndDateChangeDays,
                NewEndDate = a.NewEndDate,
                EffectiveDate = a.EffectiveDate,
                Status = a.Status,
                CreatedByUserName = a.CreatedByUser != null ? (a.CreatedByUser.FullName ?? a.CreatedByUser.UserName ?? "") : "",
                AppliedByUserName = a.AppliedByUser != null ? (a.AppliedByUser.FullName ?? a.AppliedByUser.UserName ?? "") : "",
                CreatedAt = a.CreatedAt,
                AppliedAt = a.AppliedAt,
                ApprovalRequestId = a.ApprovalRequestId
            })
            .FirstOrDefaultAsync(ct);
    }

    public async Task<ContractAmendment> CreateAmendmentAsync(
        AmendmentCreateVM model, string currentUserId, CancellationToken ct = default)
    {
        var contract = await _context.Set<Contract>()
            .FirstOrDefaultAsync(c => c.Id == model.ContractId, ct)
            ?? throw new InvalidOperationException("Contract not found.");

        var maxNum = await _context.Set<ContractAmendment>()
            .Where(a => a.ContractId == model.ContractId)
            .MaxAsync(a => (int?)a.AmendmentNumber, ct) ?? 0;

        var amendment = new ContractAmendment
        {
            ContractId = model.ContractId,
            AmendmentType = model.AmendmentType,
            AmendmentNumber = maxNum + 1,
            Title = model.Title,
            Description = model.Description,
            AmountChange = model.AmountChange,
            NewTotalAmount = contract.TotalAmount + model.AmountChange,
            EndDateChangeDays = model.EndDateChangeDays,
            NewEndDate = model.NewEndDate,
            EffectiveDate = model.EffectiveDate,
            Status = AmendmentStatus.Draft,
            CreatedByUserId = currentUserId,
            CreatedAt = DateTime.UtcNow
        };

        _context.Set<ContractAmendment>().Add(amendment);
        await _context.SaveChangesAsync(ct);
        return amendment;
    }

    public async Task<ContractAmendment?> UpdateAmendmentAsync(
        int amendmentId, AmendmentEditVM model, string currentUserId, CancellationToken ct = default)
    {
        var amendment = await _context.Set<ContractAmendment>()
            .FirstOrDefaultAsync(a => a.Id == amendmentId && a.Status == AmendmentStatus.Draft, ct);
        if (amendment == null) return null;

        amendment.Title = model.Title;
        amendment.Description = model.Description;
        amendment.AmountChange = model.AmountChange;
        amendment.EndDateChangeDays = model.EndDateChangeDays;
        amendment.NewEndDate = model.NewEndDate;
        amendment.EffectiveDate = model.EffectiveDate;

        var contract = await _context.Set<Contract>().FirstAsync(c => c.Id == amendment.ContractId, ct);
        amendment.NewTotalAmount = contract.TotalAmount + model.AmountChange;

        await _context.SaveChangesAsync(ct);
        return amendment;
    }

    public async Task<ContractAmendment?> SubmitForApprovalAsync(
        int amendmentId, string currentUserId, CancellationToken ct = default)
    {
        var amendment = await _context.Set<ContractAmendment>()
            .FirstOrDefaultAsync(a => a.Id == amendmentId && a.Status == AmendmentStatus.Draft, ct);
        if (amendment == null) return null;

        var approvalRequest = await _approvalService.CreateApprovalRequestAsync(
            amendment.ContractId, currentUserId,
            description: $"Amendment #{amendment.AmendmentNumber}: {amendment.Title}", ct);

        amendment.Status = AmendmentStatus.PendingApproval;
        amendment.ApprovalRequestId = approvalRequest.Id;
        await _context.SaveChangesAsync(ct);
        return amendment;
    }

    public async Task<ContractAmendment?> ApproveAmendmentAsync(
        int amendmentId, string currentUserId, CancellationToken ct = default)
    {
        var amendment = await _context.Set<ContractAmendment>()
            .FirstOrDefaultAsync(a => a.Id == amendmentId && a.Status == AmendmentStatus.PendingApproval, ct);
        if (amendment == null) return null;

        amendment.Status = AmendmentStatus.Approved;
        await _context.SaveChangesAsync(ct);
        return amendment;
    }

    public async Task<ContractAmendment?> ApplyAmendmentAsync(
        int amendmentId, string currentUserId, CancellationToken ct = default)
    {
        var amendment = await _context.Set<ContractAmendment>()
            .Include(a => a.Contract)
            .FirstOrDefaultAsync(a => a.Id == amendmentId && a.Status == AmendmentStatus.Approved, ct);
        if (amendment?.Contract == null) return null;

        var contract = amendment.Contract;

        await _versionService.CreateVersionSnapshotAsync(
            contract, currentUserId,
            changeType: "Amendment",
            changeDescription: amendment.Title,
            isMajor: true, ct: ct);

        if (amendment.AmountChange != 0)
            contract.TotalAmount = amendment.NewTotalAmount ?? (contract.TotalAmount + amendment.AmountChange);

        if (amendment.NewEndDate.HasValue)
            contract.EndDate = amendment.NewEndDate;
        else if (amendment.EndDateChangeDays.HasValue && contract.EndDate.HasValue)
            contract.EndDate = contract.EndDate.Value.AddDays(amendment.EndDateChangeDays.Value);

        contract.UpdatedAt = DateTime.Now;

        // Recalculate payment schedule if financial impact
        if (amendment.AmountChange != 0 || amendment.EndDateChangeDays.HasValue || amendment.NewEndDate.HasValue)
        {
            try
            {
                var existing = await _paymentScheduleService.GetScheduleAsync(contract.Id, ct);
                if (existing?.ScheduleId > 0)
                {
                    var vm = new PaymentScheduleVM
                    {
                        ContractId = contract.Id,
                        ContractNumber = contract.ContractNumber,
                        ContractTotalAmount = contract.TotalAmount,
                        ScheduleType = existing.ScheduleType,
                        InstallmentCount = existing.InstallmentCount,
                        StartDate = existing.StartDate,
                        IntervalDays = existing.IntervalDays
                    };
                    await _paymentScheduleService.DeleteScheduleAsync(existing.ScheduleId, currentUserId, ct);
                    await _paymentScheduleService.GenerateScheduleAsync(vm, currentUserId, ct);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Payment schedule regeneration failed for contract {Id}", contract.Id);
            }
        }

        amendment.Status = AmendmentStatus.Applied;
        amendment.AppliedByUserId = currentUserId;
        amendment.AppliedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);
        return amendment;
    }

    public async Task<ContractAmendment?> CancelAmendmentAsync(
        int amendmentId, string currentUserId, CancellationToken ct = default)
    {
        var amendment = await _context.Set<ContractAmendment>()
            .FirstOrDefaultAsync(a => a.Id == amendmentId
                && (a.Status == AmendmentStatus.Draft
                    || a.Status == AmendmentStatus.PendingApproval
                    || a.Status == AmendmentStatus.Approved), ct);
        if (amendment == null) return null;

        amendment.Status = AmendmentStatus.Cancelled;
        await _context.SaveChangesAsync(ct);
        return amendment;
    }

    public async Task<ContractAmendment?> RejectAmendmentAsync(
        int amendmentId, string currentUserId, string? comment = null, CancellationToken ct = default)
    {
        var amendment = await _context.Set<ContractAmendment>()
            .FirstOrDefaultAsync(a => a.Id == amendmentId && a.Status == AmendmentStatus.PendingApproval, ct);
        if (amendment == null) return null;

        amendment.Status = AmendmentStatus.Rejected;
        await _context.SaveChangesAsync(ct);
        return amendment;
    }

    private static string ToPersianDate(DateTime dt)
    {
        try
        {
            var pc = new System.Globalization.PersianCalendar();
            return $"{pc.GetYear(dt):0000}/{pc.GetMonth(dt):00}/{pc.GetDayOfMonth(dt):00}";
        }
        catch { return dt.ToString("yyyy/MM/dd"); }
    }
}
