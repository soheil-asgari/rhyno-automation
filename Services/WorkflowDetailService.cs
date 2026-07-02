using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Models;
using OfficeAutomation.Modules.Workflow.Infrastructure.Persistence;
using OfficeAutomation.Services.Security;

namespace OfficeAutomation.Services;

public sealed class WorkflowDetailService
{
    private readonly IWorkflowDbContext _context;
    private readonly IAuthorizationFacade _authorizationFacade;

    public WorkflowDetailService(IWorkflowDbContext context, IAuthorizationFacade authorizationFacade)
    {
        _context = context;
        _authorizationFacade = authorizationFacade;
    }

    public async Task<WorkflowDetailPanelVM?> BuildAsync(
        string documentType,
        int documentId,
        string title,
        string summary,
        string currentUserId,
        string approvePermission,
        CancellationToken cancellationToken = default)
    {
        var instance = await _context.WorkflowInstances
            .AsNoTracking()
            .Include(item => item.CurrentAssigneeUser)
            .Include(item => item.Steps)
            .FirstOrDefaultAsync(item => item.DocumentType == documentType && item.DocumentId == documentId, cancellationToken);

        if (instance == null)
        {
            return null;
        }

        var timeline = await _context.WorkflowActionLogs
            .AsNoTracking()
            .Include(item => item.ActorUser)
            .Where(item => item.WorkflowInstanceId == instance.Id)
            .OrderByDescending(item => item.OccurredAt)
            .ToListAsync(cancellationToken);

        var decisions = await _context.WorkflowDecisions
            .AsNoTracking()
            .Include(item => item.DecidedByUser)
            .Where(item => item.WorkflowInstanceId == instance.Id)
            .OrderByDescending(item => item.DecidedAt)
            .ToListAsync(cancellationToken);

        var comments = await _context.WorkflowComments
            .AsNoTracking()
            .Include(item => item.AuthorUser)
            .Where(item => item.WorkflowInstanceId == instance.Id)
            .OrderByDescending(item => item.CreatedAt)
            .ToListAsync(cancellationToken);

        var attachments = await _context.WorkflowAttachments
            .AsNoTracking()
            .Include(item => item.UploadedByUser)
            .Where(item => item.WorkflowInstanceId == instance.Id)
            .OrderByDescending(item => item.UploadedAt)
            .ToListAsync(cancellationToken);

        var currentStep = instance.Steps
            .Where(item => item.CompletedAt == null)
            .OrderByDescending(item => item.StepNumber)
            .FirstOrDefault();

        var canApproveModule = await _authorizationFacade.HasPermissionAsync(approvePermission, cancellationToken) ||
                               await _authorizationFacade.IsSecurityAdminAsync(cancellationToken);
        var assignedToCurrentUser = string.Equals(instance.CurrentAssigneeUserId, currentUserId, StringComparison.OrdinalIgnoreCase) ||
                                    string.Equals(currentStep?.AssignedToUserId, currentUserId, StringComparison.OrdinalIgnoreCase);
        var canAct = canApproveModule || assignedToCurrentUser;

        var userOptions = await _context.Users
            .AsNoTracking()
            .OrderBy(item => item.FullName)
            .Select(item => new WorkflowUserOptionVM
            {
                Id = item.Id,
                FullName = item.FullName ?? item.UserName ?? item.Id
            })
            .ToListAsync(cancellationToken);

        return new WorkflowDetailPanelVM
        {
            DocumentType = documentType,
            DocumentId = documentId,
            Title = title,
            Summary = summary,
            CurrentStatus = instance.CurrentStatus,
            CurrentAssigneeName = instance.CurrentAssigneeUser?.FullName,
            CurrentAssigneeId = instance.CurrentAssigneeUserId,
            SlaState = instance.SlaState ?? WorkflowSlaState.OnTrack,
            Deadline = instance.DueAt,
            CurrentStepNumber = instance.CurrentStepNumber,
            CurrentWorkflowStepId = currentStep?.Id,
            CanApprove = canAct && WorkflowStatus.IsActionPending(instance.CurrentStatus),
            CanReject = canAct && WorkflowStatus.IsActionPending(instance.CurrentStatus),
            CanReturn = canAct && instance.CurrentStepNumber > 1,
            CanRequestChanges = canAct && WorkflowStatus.IsActionPending(instance.CurrentStatus),
            CanForward = canAct && WorkflowStatus.IsActionPending(instance.CurrentStatus),
            CanDelegate = canAct && WorkflowStatus.IsActionPending(instance.CurrentStatus),
            Timeline = timeline,
            Decisions = decisions,
            Comments = comments,
            Attachments = attachments,
            UserOptions = userOptions
        };
    }
}
