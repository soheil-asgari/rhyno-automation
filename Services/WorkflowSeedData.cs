using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Models;
using OfficeAutomation.Modules.Workflow.Infrastructure.Persistence;

namespace OfficeAutomation.Services;

public static class WorkflowSeedData
{
    private const string SeedDocumentType = "SeedLetter";

    public static async Task SeedAsync(
        IWorkflowDbContext context,
        UserManager<User> userManager,
        RoleManager<ApplicationRole> roleManager,
        CancellationToken cancellationToken = default)
    {
        if (await context.WorkflowInstances.AnyAsync(item => item.DocumentType == SeedDocumentType, cancellationToken))
        {
            return;
        }

        var roles = new[]
        {
            new ApplicationRole { Name = "WorkflowRequester", DataAccessScope = RoleDataAccessScope.Department },
            new ApplicationRole { Name = "WorkflowApprover", DataAccessScope = RoleDataAccessScope.Department },
            new ApplicationRole { Name = "WorkflowReviewer", DataAccessScope = RoleDataAccessScope.Department },
            new ApplicationRole { Name = "WorkflowAdmin", DataAccessScope = RoleDataAccessScope.Global }
        };

        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role.Name!))
            {
                await roleManager.CreateAsync(role);
            }
        }

        var users = new[]
        {
            new User { Id = "seed-workflow-requester", UserName = "workflow.requester@local", Email = "workflow.requester@local", FullName = "Workflow Requester", EmailConfirmed = true, DepartmentId = 1 },
            new User { Id = "seed-workflow-approver", UserName = "workflow.approver@local", Email = "workflow.approver@local", FullName = "Workflow Approver", EmailConfirmed = true, DepartmentId = 1, IsManager = true },
            new User { Id = "seed-workflow-reviewer", UserName = "workflow.reviewer@local", Email = "workflow.reviewer@local", FullName = "Workflow Reviewer", EmailConfirmed = true, DepartmentId = 1 },
            new User { Id = "seed-workflow-delegate", UserName = "workflow.delegate@local", Email = "workflow.delegate@local", FullName = "Workflow Delegate", EmailConfirmed = true, DepartmentId = 2 },
            new User { Id = "seed-workflow-admin", UserName = "workflow.admin@local", Email = "workflow.admin@local", FullName = "Workflow Admin", EmailConfirmed = true, DepartmentId = 1 }
        };

        foreach (var user in users)
        {
            var existing = await userManager.FindByIdAsync(user.Id);
            if (existing == null)
            {
                var result = await userManager.CreateAsync(user, "Seed!2345");
                if (!result.Succeeded)
                {
                    throw new InvalidOperationException($"Failed to create workflow seed user {user.UserName}: {string.Join(" | ", result.Errors.Select(item => item.Description))}");
                }
            }
        }

        await AddToRoleAsync(userManager, "seed-workflow-requester", "WorkflowRequester");
        await AddToRoleAsync(userManager, "seed-workflow-approver", "WorkflowApprover");
        await AddToRoleAsync(userManager, "seed-workflow-reviewer", "WorkflowReviewer");
        await AddToRoleAsync(userManager, "seed-workflow-delegate", "WorkflowReviewer");
        await AddToRoleAsync(userManager, "seed-workflow-admin", "WorkflowAdmin");

        var now = DateTimeOffset.UtcNow;
        var instances = new List<WorkflowInstance>
        {
            BuildPendingInstance(1001, "Purchase request / office supplies", "seed-workflow-requester", "seed-workflow-approver", now.AddDays(2), WorkflowSlaState.OnTrack, priority: "Normal"),
            BuildPendingInstance(1002, "Contract review / due soon", "seed-workflow-requester", "seed-workflow-reviewer", now.AddHours(3), WorkflowSlaState.DueSoon, priority: "High"),
            BuildPendingInstance(1003, "Security exception / overdue", "seed-workflow-reviewer", "seed-workflow-approver", now.AddHours(-2), WorkflowSlaState.Overdue, priority: "High"),
            BuildCompletedInstance(1004, "Vendor onboarding / approved", "seed-workflow-requester", "seed-workflow-admin", WorkflowStatus.Approved, WorkflowDecisionType.Approve, now.AddDays(-1)),
            BuildCompletedInstance(1005, "Budget increase / rejected", "seed-workflow-requester", "seed-workflow-admin", WorkflowStatus.Rejected, WorkflowDecisionType.Reject, now.AddDays(-2)),
            BuildReturnedInstance(1006, now),
            BuildDelegatedInstance(1007, now)
        };

        context.WorkflowInstances.AddRange(instances);
        await context.SaveChangesAsync(cancellationToken);

        var first = instances[0].Steps[0];
        var dueSoon = instances[1].Steps[0];
        var overdue = instances[2].Steps[0];
        var approved = instances[3].Steps[0];
        var rejected = instances[4].Steps[0];
        var returnedCurrent = instances[5].Steps.Single(item => item.StepNumber == 2);
        var delegatedCurrent = instances[6].Steps.Single(item => item.StepNumber == 1);

        context.WorkflowComments.AddRange(
            new WorkflowComment { WorkflowInstanceId = instances[0].Id, WorkflowStepId = first.Id, AuthorUserId = "seed-workflow-requester", Body = "Initial request comment" },
            new WorkflowComment { WorkflowInstanceId = instances[1].Id, WorkflowStepId = dueSoon.Id, AuthorUserId = "seed-workflow-reviewer", Body = "Due soon review note" },
            new WorkflowComment { WorkflowInstanceId = instances[2].Id, WorkflowStepId = overdue.Id, AuthorUserId = "seed-workflow-approver", Body = "Overdue escalation note" },
            new WorkflowComment { WorkflowInstanceId = instances[5].Id, WorkflowStepId = returnedCurrent.Id, AuthorUserId = "seed-workflow-approver", Body = "Returned for missing commercial terms" },
            new WorkflowComment { WorkflowInstanceId = instances[6].Id, WorkflowStepId = delegatedCurrent.Id, AuthorUserId = "seed-workflow-delegate", Body = "Picked up after delegation" });

        context.WorkflowDecisions.AddRange(
            new WorkflowDecision { WorkflowInstanceId = instances[3].Id, WorkflowStepId = approved.Id, DecidedByUserId = "seed-workflow-admin", Decision = WorkflowStatus.Approved, DecisionType = WorkflowDecisionType.Approve, Comment = "Seed approved decision" },
            new WorkflowDecision { WorkflowInstanceId = instances[4].Id, WorkflowStepId = rejected.Id, DecidedByUserId = "seed-workflow-admin", Decision = WorkflowStatus.Rejected, DecisionType = WorkflowDecisionType.Reject, Comment = "Seed rejected decision" },
            new WorkflowDecision { WorkflowInstanceId = instances[0].Id, WorkflowStepId = first.Id, DecidedByUserId = "seed-workflow-approver", Decision = WorkflowStatus.PendingApproval, DecisionType = WorkflowDecisionType.Comment, Comment = "Seed pending decision note" },
            new WorkflowDecision { WorkflowInstanceId = instances[5].Id, WorkflowStepId = returnedCurrent.Id, DecidedByUserId = "seed-workflow-reviewer", Decision = WorkflowStatus.Returned, DecisionType = WorkflowDecisionType.Return, Comment = "Returned to step one for revision" },
            new WorkflowDecision { WorkflowInstanceId = instances[6].Id, WorkflowStepId = delegatedCurrent.Id, DecidedByUserId = "seed-workflow-approver", Decision = WorkflowStatus.PendingApproval, DecisionType = WorkflowDecisionType.Delegate, Comment = "Delegated to backup reviewer" });

        context.WorkflowAttachments.AddRange(
            new WorkflowAttachment { WorkflowInstanceId = instances[0].Id, WorkflowStepId = first.Id, UploadedByUserId = "seed-workflow-requester", FileName = "seed-request.pdf", FilePath = "/uploads/workflow/seed/request.pdf", ContentType = "application/pdf", FileSize = 1024 },
            new WorkflowAttachment { WorkflowInstanceId = instances[2].Id, WorkflowStepId = overdue.Id, UploadedByUserId = "seed-workflow-approver", FileName = "overdue-note.txt", FilePath = "/uploads/workflow/seed/overdue-note.txt", ContentType = "text/plain", FileSize = 256 },
            new WorkflowAttachment { WorkflowInstanceId = instances[5].Id, WorkflowStepId = returnedCurrent.Id, UploadedByUserId = "seed-workflow-requester", FileName = "revised-proposal.docx", FilePath = "/uploads/workflow/seed/revised-proposal.docx", ContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document", FileSize = 40960 });

        context.WorkflowActionLogs.AddRange(
            new WorkflowActionLog { WorkflowInstanceId = instances[0].Id, WorkflowStepId = first.Id, ActorUserId = "seed-workflow-requester", ActionType = WorkflowDecisionType.Start, Description = "Seed workflow started" },
            new WorkflowActionLog { WorkflowInstanceId = instances[1].Id, WorkflowStepId = dueSoon.Id, ActorUserId = "seed-workflow-reviewer", ActionType = WorkflowDecisionType.Comment, Description = "Seed due soon comment" },
            new WorkflowActionLog { WorkflowInstanceId = instances[2].Id, WorkflowStepId = overdue.Id, ActorUserId = "seed-workflow-approver", ActionType = WorkflowDecisionType.Escalate, Description = "Seed overdue escalation" },
            new WorkflowActionLog { WorkflowInstanceId = instances[5].Id, WorkflowStepId = returnedCurrent.Id, ActorUserId = "seed-workflow-reviewer", ActionType = WorkflowDecisionType.Return, Description = "Returned to requester for updates" },
            new WorkflowActionLog { WorkflowInstanceId = instances[6].Id, WorkflowStepId = delegatedCurrent.Id, ActorUserId = "seed-workflow-approver", ActionType = WorkflowDecisionType.Delegate, Description = "Delegated to backup reviewer" });

        await context.SaveChangesAsync(cancellationToken);
    }

    private static WorkflowInstance BuildPendingInstance(
        int documentId,
        string title,
        string startedByUserId,
        string assigneeUserId,
        DateTimeOffset dueAt,
        string slaState,
        string priority)
    {
        var now = DateTimeOffset.UtcNow;
        return new WorkflowInstance
        {
            DocumentType = SeedDocumentType,
            DocumentId = documentId,
            Status = WorkflowStatus.PendingApproval,
            CurrentStatus = WorkflowStatus.PendingApproval,
            CurrentStepNumber = 1,
            Priority = priority,
            StartedByUserId = startedByUserId,
            CurrentAssigneeUserId = assigneeUserId,
            DueAt = dueAt,
            SlaState = slaState,
            LastActionAt = now,
            ActionLogs =
            [
                new WorkflowActionLog
                {
                    ActorUserId = startedByUserId,
                    ActionType = WorkflowDecisionType.Start,
                    Description = title,
                    OccurredAt = now.AddMinutes(-30)
                }
            ],
            Steps =
            [
                new WorkflowStep
                {
                    StepNumber = 1,
                    StepName = "Seed approval",
                    AssignmentMode = WorkflowAssignmentMode.User,
                    AssignedToUserId = assigneeUserId,
                    Status = WorkflowStatus.PendingApproval,
                    DueAt = dueAt,
                    SlaState = slaState,
                    ReadAt = now.AddMinutes(-10)
                }
            ]
        };
    }

    private static WorkflowInstance BuildCompletedInstance(
        int documentId,
        string title,
        string startedByUserId,
        string completedByUserId,
        string status,
        string decisionType,
        DateTimeOffset dueAt)
    {
        var now = DateTimeOffset.UtcNow;
        return new WorkflowInstance
        {
            DocumentType = SeedDocumentType,
            DocumentId = documentId,
            Status = status,
            CurrentStatus = status,
            CurrentStepNumber = 1,
            Priority = "Normal",
            StartedByUserId = startedByUserId,
            DueAt = dueAt,
            SlaState = WorkflowSlaState.OnTrack,
            CompletedAt = now,
            ClosedAt = now,
            LastActionAt = now,
            Steps =
            [
                new WorkflowStep
                {
                    StepNumber = 1,
                    StepName = "Final approval",
                    AssignmentMode = WorkflowAssignmentMode.User,
                    AssignedToUserId = completedByUserId,
                    Status = status,
                    DueAt = dueAt,
                    SlaState = WorkflowSlaState.OnTrack,
                    CompletedAt = now,
                    ReadAt = now.AddMinutes(-5)
                }
            ],
            ActionLogs =
            [
                new WorkflowActionLog
                {
                    ActorUserId = startedByUserId,
                    ActionType = WorkflowDecisionType.Start,
                    Description = title,
                    OccurredAt = now.AddHours(-4)
                },
                new WorkflowActionLog
                {
                    ActorUserId = completedByUserId,
                    ActionType = decisionType,
                    Description = $"Completed as {status}",
                    OccurredAt = now.AddHours(-1)
                }
            ]
        };
    }

    private static WorkflowInstance BuildReturnedInstance(int documentId, DateTimeOffset now)
    {
        return new WorkflowInstance
        {
            DocumentType = SeedDocumentType,
            DocumentId = documentId,
            Status = WorkflowStatus.Returned,
            CurrentStatus = WorkflowStatus.Returned,
            CurrentStepNumber = 2,
            Priority = "High",
            StartedByUserId = "seed-workflow-requester",
            CurrentAssigneeUserId = "seed-workflow-reviewer",
            DueAt = now.AddDays(1),
            SlaState = WorkflowSlaState.OnTrack,
            LastActionAt = now.AddMinutes(-20),
            Steps =
            [
                new WorkflowStep
                {
                    StepNumber = 1,
                    StepName = "Requester submission",
                    AssignmentMode = WorkflowAssignmentMode.User,
                    AssignedToUserId = "seed-workflow-approver",
                    Status = WorkflowStatus.Approved,
                    CompletedAt = now.AddHours(-8),
                    ReadAt = now.AddHours(-8)
                },
                new WorkflowStep
                {
                    StepNumber = 2,
                    StepName = "Reviewer validation",
                    AssignmentMode = WorkflowAssignmentMode.User,
                    AssignedToUserId = "seed-workflow-reviewer",
                    Status = WorkflowStatus.Returned,
                    DueAt = now.AddDays(1),
                    ReturnedFromStepNumber = 2,
                    ReadAt = now.AddHours(-1)
                }
            ]
        };
    }

    private static WorkflowInstance BuildDelegatedInstance(int documentId, DateTimeOffset now)
    {
        return new WorkflowInstance
        {
            DocumentType = SeedDocumentType,
            DocumentId = documentId,
            Status = WorkflowStatus.PendingApproval,
            CurrentStatus = WorkflowStatus.PendingApproval,
            CurrentStepNumber = 1,
            Priority = "Normal",
            StartedByUserId = "seed-workflow-requester",
            CurrentAssigneeUserId = "seed-workflow-delegate",
            DueAt = now.AddHours(5),
            SlaState = WorkflowSlaState.DueSoon,
            LastActionAt = now.AddMinutes(-15),
            Steps =
            [
                new WorkflowStep
                {
                    StepNumber = 1,
                    StepName = "Delegate review",
                    AssignmentMode = WorkflowAssignmentMode.User,
                    AssignedToUserId = "seed-workflow-delegate",
                    DelegatedFromUserId = "seed-workflow-approver",
                    Status = WorkflowStatus.PendingApproval,
                    DueAt = now.AddHours(5),
                    SlaState = WorkflowSlaState.DueSoon,
                    ReadAt = now.AddMinutes(-5)
                }
            ],
            Escalations =
            [
                new WorkflowEscalationEvent
                {
                    WorkflowStepId = null,
                    EscalatedToUserId = "seed-workflow-delegate",
                    PreviousSlaState = WorkflowSlaState.OnTrack,
                    NewSlaState = WorkflowSlaState.DueSoon,
                    Note = "Backup reviewer assigned for coverage",
                    EscalatedAt = now.AddMinutes(-10)
                }
            ]
        };
    }

    private static async Task AddToRoleAsync(UserManager<User> userManager, string userId, string roleName)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user != null && !await userManager.IsInRoleAsync(user, roleName))
        {
            await userManager.AddToRoleAsync(user, roleName);
        }
    }
}
