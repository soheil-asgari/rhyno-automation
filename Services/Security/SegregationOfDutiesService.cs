using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Modules.Identity.Infrastructure.Persistence;
using OfficeAutomation.Modules.Workflow.Infrastructure.Persistence;
using OfficeAutomation.Models;

namespace OfficeAutomation.Services.Security;

public interface ISegregationOfDutiesService
{
    Task<(bool Allowed, string? Reason)> ValidateRoleAssignmentAsync(string userId, IEnumerable<string> candidateRoles, CancellationToken cancellationToken = default);
    Task<(bool Allowed, string? Reason)> ValidateFinanceApprovalAsync(Invoice invoice, string? approverUserId, CancellationToken cancellationToken = default);
}

public sealed class SegregationOfDutiesService : ISegregationOfDutiesService
{
    private static readonly RoleConflictRule[] DefaultRules =
    [
        new() { RoleA = "FinanceRequester", RoleB = "FinanceApprover", Reason = "Requester and approver roles cannot be combined." },
        new() { RoleA = "PayrollCreator", RoleB = "PayrollApprover", Reason = "Payroll preparation and final approval must be separated." },
        new() { RoleA = "WarehouseRequester", RoleB = "WarehouseApprover", Reason = "Warehouse requester and approver must be different users." }
    ];

    private readonly UserManager<User> _userManager;
    private readonly IdentityDbContext _context;
    private readonly WorkflowDbContext _workflowContext;

    public SegregationOfDutiesService(
        UserManager<User> userManager,
        IdentityDbContext context,
        WorkflowDbContext workflowContext)
    {
        _userManager = userManager;
        _context = context;
        _workflowContext = workflowContext;
    }

    public async Task<(bool Allowed, string? Reason)> ValidateRoleAssignmentAsync(string userId, IEnumerable<string> candidateRoles, CancellationToken cancellationToken = default)
    {
        List<string> currentRoles = [];
        if (!string.IsNullOrWhiteSpace(userId))
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user != null)
            {
                currentRoles = (await _userManager.GetRolesAsync(user)).ToList();
            }
        }

        var allRoles = currentRoles.Concat(candidateRoles).Where(item => !string.IsNullOrWhiteSpace(item)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        foreach (var rule in DefaultRules)
        {
            if (allRoles.Contains(rule.RoleA, StringComparer.OrdinalIgnoreCase) &&
                allRoles.Contains(rule.RoleB, StringComparer.OrdinalIgnoreCase))
            {
                return (false, rule.Reason);
            }
        }

        return (true, null);
    }

    public async Task<(bool Allowed, string? Reason)> ValidateFinanceApprovalAsync(Invoice invoice, string? approverUserId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(approverUserId))
        {
            return (false, "Approver context is missing.");
        }

        if (!string.IsNullOrWhiteSpace(invoice.CreatedByUserId) &&
            string.Equals(invoice.CreatedByUserId, approverUserId, StringComparison.OrdinalIgnoreCase))
        {
            return (false, "Creator cannot approve the same invoice.");
        }

        var startedBy = await _workflowContext.WorkflowInstances
            .AsNoTracking()
            .Where(item => item.DocumentType == "Invoice" && item.DocumentId == invoice.Id)
            .OrderBy(item => item.Id)
            .Select(item => item.StartedByUserId)
            .FirstOrDefaultAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(startedBy) &&
            string.Equals(startedBy, approverUserId, StringComparison.OrdinalIgnoreCase))
        {
            return (false, "Workflow requester cannot perform the final finance approval.");
        }

        return (true, null);
    }
}

