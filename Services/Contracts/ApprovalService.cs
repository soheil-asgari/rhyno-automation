using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Models;
using OfficeAutomation.Modules.Office.Infrastructure.Persistence;

namespace OfficeAutomation.Services.Contracts;

public sealed class ApprovalService : IApprovalService
{
    private readonly OfficeDbContext _context;
    private readonly UserManager<User> _userManager;
    private readonly IContractService _contractService;
    private readonly ILogger<ApprovalService> _logger;

    // Threshold constants (in the base currency — IRR/Toman handled upstream)
    private const decimal ManagerThreshold = 10_000m;
    private const decimal DirectorThreshold = 100_000m;
    // >= 100K = VP

    public ApprovalService(
        OfficeDbContext context,
        UserManager<User> userManager,
        IContractService contractService,
        ILogger<ApprovalService> logger)
    {
        _context = context;
        _userManager = userManager;
        _contractService = contractService;
        _logger = logger;
    }

    // ═══════════════════════════════════════════════════════════════
    //  Helpers
    // ═══════════════════════════════════════════════════════════════

    private async Task<bool> IsAdminAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return false;
        return await _userManager.IsInRoleAsync(user, "Admin");
    }

    private async Task AddAuditLogAsync(int contractId, string userId, string action,
        string? oldStatus, string? newStatus, string? description = null,
        CancellationToken ct = default)
    {
        var auditLog = new ContractAuditLog
        {
            ContractId = contractId,
            UserId = userId,
            Action = action,
            OldStatus = oldStatus,
            NewStatus = newStatus,
            Description = description,
            CreatedAt = DateTime.Now
        };
        _context.ContractAuditLogs.Add(auditLog);
        await _context.SaveChangesAsync(ct);
    }

    /// <summary>
    /// رفع ارجاع: اگر کاربر اصلی delegation فعال داشته باشد، کاربر جایگزین را برمی‌گرداند.
    /// </summary>
    public async Task<string?> ResolveDelegationAsync(
        string assignedUserId, string? contractType = null,
        CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var delegation = await _context.Set<ContractApprovalDelegation>()
            .AsNoTracking()
            .Where(d => d.FromUserId == assignedUserId
                     && d.IsActive
                     && d.StartsAt <= now
                     && d.EndsAt >= now)
            .Where(d => string.IsNullOrEmpty(contractType) || d.ContractType == null || d.ContractType == contractType)
            .OrderByDescending(d => d.CreatedAt)
            .FirstOrDefaultAsync(ct);

        return delegation?.ToUserId;
    }

    // ═══════════════════════════════════════════════════════════════
    //  Build Approval Steps by Amount (Threshold-based routing)
    // ═══════════════════════════════════════════════════════════════

    public async Task<List<ContractApprovalStep>> BuildApprovalStepsForAmountAsync(
        int requestId, decimal amount, string? contractType = null,
        CancellationToken ct = default)
    {
        var steps = new List<ContractApprovalStep>();

        steps.Add(new ContractApprovalStep
        {
            ContractApprovalRequestId = requestId,
            StepNumber = 1,
            StepTitle = "مدیر مستقیم",
            AssigneeType = ApprovalStepAssigneeType.Threshold,
            MinThreshold = 0,
            MaxThreshold = ManagerThreshold,
            IsRequired = true,
            Decision = ApprovalStepDecision.Pending,
            CreatedAt = DateTime.UtcNow
        });

        if (amount > ManagerThreshold)
        {
            steps.Add(new ContractApprovalStep
            {
                ContractApprovalRequestId = requestId,
                StepNumber = 2,
                StepTitle = "مدیر مالی / مدیر بخش",
                AssigneeType = ApprovalStepAssigneeType.Threshold,
                MinThreshold = ManagerThreshold + 0.01m,
                MaxThreshold = DirectorThreshold,
                IsRequired = true,
                Decision = ApprovalStepDecision.Pending,
                CreatedAt = DateTime.UtcNow
            });
        }

        if (amount > DirectorThreshold)
        {
            steps.Add(new ContractApprovalStep
            {
                ContractApprovalRequestId = requestId,
                StepNumber = 3,
                StepTitle = "معاون / مدیرعامل",
                AssigneeType = ApprovalStepAssigneeType.Threshold,
                MinThreshold = DirectorThreshold + 0.01m,
                MaxThreshold = null,
                IsRequired = true,
                Decision = ApprovalStepDecision.Pending,
                CreatedAt = DateTime.UtcNow
            });
        }

        return steps;
    }

    // ═══════════════════════════════════════════════════════════════
    //  Create Approval Request
    // ═══════════════════════════════════════════════════════════════

    public async Task<ContractApprovalRequest> CreateApprovalRequestAsync(
        int contractId, string currentUserId,
        string? description = null, CancellationToken ct = default)
    {
        var contract = await _context.Contracts
            .FirstOrDefaultAsync(c => c.Id == contractId, ct)
            ?? throw new KeyNotFoundException($"Contract {contractId} not found.");

        var activeRequest = await _context.Set<ContractApprovalRequest>()
            .AsNoTracking()
            .AnyAsync(r => r.ContractId == contractId
                       && (r.Status == ApprovalRequestStatus.Pending
                        || r.Status == ApprovalRequestStatus.PartiallyApproved), ct);
        if (activeRequest)
            throw new InvalidOperationException("این قرارداد در حال حاضر یک درخواست تایید فعال دارد.");

        var request = new ContractApprovalRequest
        {
            ContractId = contractId,
            RequestedByUserId = currentUserId,
            Status = ApprovalRequestStatus.Pending,
            ApprovalMode = ApprovalMode.Sequential,
            SubmittedAmount = contract.TotalAmount,
            Description = description,
            CurrentStepNumber = 1,
            CreatedAt = DateTime.UtcNow
        };

        _context.Set<ContractApprovalRequest>().Add(request);
        await _context.SaveChangesAsync(ct);

        var steps = await BuildApprovalStepsForAmountAsync(request.Id, contract.TotalAmount, contract.ContractType, ct);
        _context.Set<ContractApprovalStep>().AddRange(steps);
        await _context.SaveChangesAsync(ct);

        var oldStatus = contract.Status;
        contract.Status = ContractStatuses.PendingApproval;
        contract.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);

        await AddAuditLogAsync(
            contractId, currentUserId,
            "ApprovalRequestCreated",
            oldStatus, ContractStatuses.PendingApproval,
            $"درخواست تایید با {steps.Count} مرحله ایجاد شد. مبلغ: {contract.TotalAmount:N0}",
            ct);

        _logger.LogInformation(
            "Approval request {RequestId} created for contract {ContractId} by {UserId} with {StepCount} steps",
            request.Id, contractId, currentUserId, steps.Count);

        return request;
    }

    // ═══════════════════════════════════════════════════════════════
    //  Get Approval Request Methods
    // ═══════════════════════════════════════════════════════════════

    public async Task<ContractApprovalRequest?> GetApprovalRequestByIdAsync(
        int requestId, string currentUserId, CancellationToken ct = default)
    {
        return await _context.Set<ContractApprovalRequest>()
            .AsNoTracking()
            .Include(r => r.Contract)
            .Include(r => r.RequestedByUser)
            .Include(r => r.Steps)
                .ThenInclude(s => s.AssignedUser)
            .Include(r => r.Steps)
                .ThenInclude(s => s.DecidedByUser)
            .Include(r => r.Steps)
                .ThenInclude(s => s.DelegatedToUser)
            .FirstOrDefaultAsync(r => r.Id == requestId, ct);
    }

    public async Task<ContractApprovalRequest?> GetActiveApprovalRequestForContractAsync(
        int contractId, CancellationToken ct = default)
    {
        return await _context.Set<ContractApprovalRequest>()
            .AsNoTracking()
            .Include(r => r.Steps)
            .Where(r => r.ContractId == contractId
                     && (r.Status == ApprovalRequestStatus.Pending
                      || r.Status == ApprovalRequestStatus.PartiallyApproved))
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<List<ContractApprovalRequest>> GetApprovalRequestsForContractAsync(
        int contractId, CancellationToken ct = default)
    {
        return await _context.Set<ContractApprovalRequest>()
            .AsNoTracking()
            .Include(r => r.RequestedByUser)
            .Include(r => r.Steps)
            .Where(r => r.ContractId == contractId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<List<ContractApprovalRequest>> GetPendingApprovalsForUserAsync(
        string userId, CancellationToken ct = default)
    {
        var delegatedStepIds = await _context.Set<ContractApprovalStep>()
            .AsNoTracking()
            .Where(s => s.DelegatedToUserId == userId
                     && s.Decision == ApprovalStepDecision.Pending)
            .Select(s => s.Id)
            .ToListAsync(ct);

        var directStepIds = await _context.Set<ContractApprovalStep>()
            .AsNoTracking()
            .Where(s => s.AssignedUserId == userId
                     && s.Decision == ApprovalStepDecision.Pending)
            .Select(s => s.Id)
            .ToListAsync(ct);

        var allStepIds = delegatedStepIds.Concat(directStepIds).Distinct().ToList();

        if (allStepIds.Count == 0)
            return new List<ContractApprovalRequest>();

        return await _context.Set<ContractApprovalRequest>()
            .AsNoTracking()
            .Include(r => r.Contract)
            .Include(r => r.RequestedByUser)
            .Include(r => r.Steps)
            .Where(r => (r.Status == ApprovalRequestStatus.Pending
                      || r.Status == ApprovalRequestStatus.PartiallyApproved)
                     && r.Steps.Any(s => allStepIds.Contains(s.Id)))
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);
    }
// ═══════════════════════════════════════════════════════════════
    //  Approve Step
    // ═══════════════════════════════════════════════════════════════

    public async Task<ContractApprovalStep> ApproveStepAsync(
        int stepId, string currentUserId,
        string? comment = null, CancellationToken ct = default)
    {
        var step = await _context.Set<ContractApprovalStep>()
            .Include(s => s.ContractApprovalRequest)
            .FirstOrDefaultAsync(s => s.Id == stepId, ct)
            ?? throw new KeyNotFoundException($"Approval step {stepId} not found.");

        if (step.Decision != ApprovalStepDecision.Pending)
            throw new InvalidOperationException("این مرحله قبلاً تصمیم‌گیری شده است.");

        var isAssigned = step.AssignedUserId == currentUserId;
        var isDelegated = step.DelegatedToUserId == currentUserId;
        if (!isAssigned && !isDelegated)
            throw new UnauthorizedAccessException("شما مجاز به تایید این مرحله نیستید.");

        var request = step.ContractApprovalRequest!;

        // Record the decision
        step.Decision = ApprovalStepDecision.Approved;
        step.DecidedByUserId = currentUserId;
        step.Comment = comment;
        step.DecidedAt = DateTime.UtcNow;

        if (request.ApprovalMode == ApprovalMode.Sequential)
        {
            var nextPendingStep = await _context.Set<ContractApprovalStep>()
                .Where(s => s.ContractApprovalRequestId == request.Id
                         && s.StepNumber > step.StepNumber
                         && s.Decision == ApprovalStepDecision.Pending)
                .OrderBy(s => s.StepNumber)
                .FirstOrDefaultAsync(ct);

            if (nextPendingStep == null)
            {
                request.Status = ApprovalRequestStatus.Approved;
                request.CompletedAt = DateTime.UtcNow;

                var contract = await _context.Contracts
                    .FirstOrDefaultAsync(c => c.Id == request.ContractId, ct);
                if (contract != null)
                {
                    contract.Status = ContractStatuses.Active;
                    contract.UpdatedAt = DateTime.UtcNow;
                }

                await AddAuditLogAsync(
                    request.ContractId, currentUserId,
                    "ApprovalCompleted",
                    ContractStatuses.PendingApproval, ContractStatuses.Active,
                    "تمامی مراحل تایید با موفقیت انجام شد.",
                    ct);
            }
            else
            {
                request.CurrentStepNumber = nextPendingStep.StepNumber;
            }
        }
        else // Parallel mode
        {
            var pendingStepsInSameGroup = await _context.Set<ContractApprovalStep>()
                .Where(s => s.ContractApprovalRequestId == request.Id
                         && s.StepNumber == step.StepNumber
                         && s.Decision == ApprovalStepDecision.Pending)
                .ToListAsync(ct);

            pendingStepsInSameGroup.RemoveAll(s => s.Id == stepId);

            if (pendingStepsInSameGroup.Count == 0)
            {
                var nextStepNumber = request.CurrentStepNumber + 1;
                var nextStepsExist = await _context.Set<ContractApprovalStep>()
                    .AnyAsync(s => s.ContractApprovalRequestId == request.Id
                               && s.StepNumber == nextStepNumber, ct);

                if (!nextStepsExist)
                {
                    request.Status = ApprovalRequestStatus.Approved;
                    request.CompletedAt = DateTime.UtcNow;

                    var contract = await _context.Contracts
                        .FirstOrDefaultAsync(c => c.Id == request.ContractId, ct);
                    if (contract != null)
                    {
                        contract.Status = ContractStatuses.Active;
                        contract.UpdatedAt = DateTime.UtcNow;
                    }

                    await AddAuditLogAsync(
                        request.ContractId, currentUserId,
                        "ApprovalCompleted",
                        ContractStatuses.PendingApproval, ContractStatuses.Active,
                        "تمامی مراحل تایید موازی با موفقیت انجام شد.",
                        ct);
                }
                else
                {
                    request.CurrentStepNumber = nextStepNumber;
                }
            }
        }

        await _context.SaveChangesAsync(ct);
        return step;
    }
// ═══════════════════════════════════════════════════════════════
    //  Reject Step
    // ═══════════════════════════════════════════════════════════════

    public async Task<ContractApprovalStep> RejectStepAsync(
        int stepId, string currentUserId,
        string comment, string returnMode = "Draft",
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(comment))
            throw new ArgumentException("دلیل رد کردن اجباری است.");

        var step = await _context.Set<ContractApprovalStep>()
            .Include(s => s.ContractApprovalRequest)
            .FirstOrDefaultAsync(s => s.Id == stepId, ct)
            ?? throw new KeyNotFoundException($"Approval step {stepId} not found.");

        if (step.Decision != ApprovalStepDecision.Pending)
            throw new InvalidOperationException("این مرحله قبلاً تصمیم‌گیری شده است.");

        var isAssigned = step.AssignedUserId == currentUserId;
        var isDelegated = step.DelegatedToUserId == currentUserId;
        if (!isAssigned && !isDelegated)
            throw new UnauthorizedAccessException("شما مجاز به رد این مرحله نیستید.");

        var request = step.ContractApprovalRequest!;

        // Record rejection
        step.Decision = ApprovalStepDecision.Rejected;
        step.DecidedByUserId = currentUserId;
        step.Comment = comment;
        step.DecidedAt = DateTime.UtcNow;

        request.Status = ApprovalRequestStatus.Rejected;
        request.CompletedAt = DateTime.UtcNow;
        request.FinalComment = comment;
        request.RejectionReturnMode = returnMode;

        var contract = await _context.Contracts
            .FirstOrDefaultAsync(c => c.Id == request.ContractId, ct);

        if (contract != null)
        {
            var oldStatus = contract.Status;

            if (returnMode.Equals("Draft", StringComparison.OrdinalIgnoreCase))
            {
                contract.Status = ContractStatuses.Draft;
            }
            else // "PreviousStage"
            {
                var previousStep = await _context.Set<ContractApprovalStep>()
                    .Where(s => s.ContractApprovalRequestId == request.Id
                             && s.StepNumber < step.StepNumber
                             && s.Decision == ApprovalStepDecision.Approved)
                    .OrderByDescending(s => s.StepNumber)
                    .FirstOrDefaultAsync(ct);

                if (previousStep != null)
                {
                    var laterSteps = await _context.Set<ContractApprovalStep>()
                        .Where(s => s.ContractApprovalRequestId == request.Id
                                 && s.StepNumber >= step.StepNumber)
                        .ToListAsync(ct);

                    foreach (var laterStep in laterSteps)
                    {
                        laterStep.Decision = ApprovalStepDecision.Pending;
                        laterStep.DecidedByUserId = null;
                        laterStep.Comment = null;
                        laterStep.DecidedAt = null;
                    }

                    request.Status = ApprovalRequestStatus.Pending;
                    request.CompletedAt = null;
                    request.FinalComment = null;
                    request.CurrentStepNumber = step.StepNumber;
                    contract.Status = ContractStatuses.PendingApproval;
                }
                else
                {
                    contract.Status = ContractStatuses.Draft;
                }
            }

            contract.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(ct);

        await AddAuditLogAsync(
            request.ContractId, currentUserId,
            "ApprovalRejected",
            ContractStatuses.PendingApproval,
            contract?.Status,
            $"مرحله {step.StepTitle} رد شد. دلیل: {comment}. بازگشت به: {returnMode}",
            ct);

        return step;
    }
// ═══════════════════════════════════════════════════════════════
    //  Delegate Step
    // ═══════════════════════════════════════════════════════════════

    public async Task<ContractApprovalStep> DelegateStepAsync(
        int stepId, string currentUserId,
        string delegateToUserId, string? reason = null,
        CancellationToken ct = default)
    {
        var step = await _context.Set<ContractApprovalStep>()
            .Include(s => s.ContractApprovalRequest)
            .FirstOrDefaultAsync(s => s.Id == stepId, ct)
            ?? throw new KeyNotFoundException($"Approval step {stepId} not found.");

        if (step.Decision != ApprovalStepDecision.Pending)
            throw new InvalidOperationException("این مرحله قبلاً تصمیم‌گیری شده است.");

        if (step.AssignedUserId != currentUserId && !await IsAdminAsync(currentUserId))
            throw new UnauthorizedAccessException("شما مجاز به ارجاع این مرحله نیستید.");

        var targetUser = await _userManager.FindByIdAsync(delegateToUserId);
        if (targetUser == null)
            throw new KeyNotFoundException("کاربر مقصد برای ارجاع یافت نشد.");

        step.DelegatedToUserId = delegateToUserId;
        step.Decision = ApprovalStepDecision.Delegated;
        step.Comment = reason;
        step.DecidedAt = DateTime.UtcNow;

        var delegateStep = new ContractApprovalStep
        {
            ContractApprovalRequestId = step.ContractApprovalRequestId,
            StepNumber = step.StepNumber,
            StepTitle = step.StepTitle + " (ارجاع شده)",
            AssigneeType = ApprovalStepAssigneeType.User,
            AssignedUserId = delegateToUserId,
            MinThreshold = step.MinThreshold,
            MaxThreshold = step.MaxThreshold,
            IsRequired = step.IsRequired,
            Decision = ApprovalStepDecision.Pending,
            CreatedAt = DateTime.UtcNow
        };

        _context.Set<ContractApprovalStep>().Add(delegateStep);
        await _context.SaveChangesAsync(ct);

        return step;
    }

    // ═══════════════════════════════════════════════════════════════
    //  Cancel Approval Request
    // ═══════════════════════════════════════════════════════════════

    public async Task<bool> CancelApprovalRequestAsync(
        int requestId, string currentUserId, CancellationToken ct = default)
    {
        var request = await _context.Set<ContractApprovalRequest>()
            .FirstOrDefaultAsync(r => r.Id == requestId, ct);

        if (request == null) return false;

        if (request.RequestedByUserId != currentUserId && !await IsAdminAsync(currentUserId))
            throw new UnauthorizedAccessException("شما مجاز به لغو این درخواست نیستید.");

        if (request.Status == ApprovalRequestStatus.Approved
            || request.Status == ApprovalRequestStatus.Rejected
            || request.Status == ApprovalRequestStatus.Cancelled)
            throw new InvalidOperationException("این درخواست قابل لغو نیست.");

        request.Status = ApprovalRequestStatus.Cancelled;
        request.CompletedAt = DateTime.UtcNow;

        var contract = await _context.Contracts
            .FirstOrDefaultAsync(c => c.Id == request.ContractId, ct);
        if (contract != null && contract.Status == ContractStatuses.PendingApproval)
        {
            contract.Status = ContractStatuses.Draft;
            contract.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(ct);

        await AddAuditLogAsync(
            request.ContractId, currentUserId,
            "ApprovalCancelled",
            ContractStatuses.PendingApproval, ContractStatuses.Draft,
            "درخواست تایید لغو شد.",
            ct);

        return true;
    }

    // ═══════════════════════════════════════════════════════════════
    //  Delegation Management
    // ═══════════════════════════════════════════════════════════════

    public async Task<ContractApprovalDelegation> CreateDelegationAsync(
        string fromUserId, string toUserId,
        DateTime startsAt, DateTime endsAt,
        string? contractType = null, string? reason = null,
        CancellationToken ct = default)
    {
        var delegation = new ContractApprovalDelegation
        {
            FromUserId = fromUserId,
            ToUserId = toUserId,
            ContractType = contractType,
            StartsAt = startsAt,
            EndsAt = endsAt,
            IsActive = true,
            Reason = reason,
            CreatedAt = DateTime.UtcNow
        };

        _context.Set<ContractApprovalDelegation>().Add(delegation);
        await _context.SaveChangesAsync(ct);
        return delegation;
    }

    public async Task<bool> CancelDelegationAsync(
        int delegationId, string currentUserId, CancellationToken ct = default)
    {
        var delegation = await _context.Set<ContractApprovalDelegation>()
            .FirstOrDefaultAsync(d => d.Id == delegationId, ct);

        if (delegation == null) return false;
        if (delegation.FromUserId != currentUserId && !await IsAdminAsync(currentUserId))
            throw new UnauthorizedAccessException("شما مجاز به لغو این ارجاع نیستید.");

        delegation.IsActive = false;
        await _context.SaveChangesAsync(ct);
        return true;
    }

    public async Task<List<ContractApprovalDelegation>> GetActiveDelegationsForUserAsync(
        string userId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        return await _context.Set<ContractApprovalDelegation>()
            .AsNoTracking()
            .Include(d => d.ToUser)
            .Where(d => d.FromUserId == userId
                     && d.IsActive
                     && d.StartsAt <= now
                     && d.EndsAt >= now)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync(ct);
    }
}