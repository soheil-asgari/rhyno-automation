using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using OfficeAutomation.Models;

namespace OfficeAutomation.Modules.Workflow.Infrastructure.Persistence;

public interface IWorkflowDbContext
{
    IModel Model { get; }
    DbSet<WorkflowRoute> WorkflowRoutes { get; }
    DbSet<WorkflowDefinitionVersion> WorkflowDefinitionVersions { get; }
    DbSet<WorkflowStepDefinition> WorkflowStepDefinitions { get; }
    DbSet<WorkflowRule> WorkflowRules { get; }
    DbSet<WorkflowInstance> WorkflowInstances { get; }
    DbSet<WorkflowStep> WorkflowSteps { get; }
    DbSet<WorkflowDecision> WorkflowDecisions { get; }
    DbSet<WorkflowDelegation> WorkflowDelegations { get; }
    DbSet<WorkflowActionLog> WorkflowActionLogs { get; }
    DbSet<WorkflowAttachment> WorkflowAttachments { get; }
    DbSet<WorkflowComment> WorkflowComments { get; }
    DbSet<WorkflowEscalationEvent> WorkflowEscalationEvents { get; }
    DbSet<WorkflowSlaJob> WorkflowSlaJobs { get; }
    DbSet<WorkflowCaseTask> WorkflowCaseTasks { get; }
    DbSet<WorkflowTransitionEvent> WorkflowTransitionEvents { get; }
    DbSet<WorkflowIncident> WorkflowIncidents { get; }
    DbSet<DocumentSignature> DocumentSignatures { get; }
    DbSet<ConnectorDeadLetterMessage> ConnectorDeadLetterMessages { get; }
    DbSet<ConnectorExecutionLog> ConnectorExecutionLogs { get; }
    DbSet<OutboxMessage> OutboxMessages { get; }
    DbSet<User> Users { get; }
    DbSet<ApplicationRole> Roles { get; }
    DbSet<Department> Departments { get; }
    DbSet<SystemSetting> SystemSettings { get; }
    DbSet<DocumentArchiveItem> DocumentArchiveItems { get; }
    DbSet<Letter> Letters { get; }
    DbSet<Leave> Leaves { get; }
    DbSet<Notification> Notifications { get; }

    ValueTask<object?> FindAsync(Type entityType, object?[]? keyValues, CancellationToken cancellationToken);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
