using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Data;
using OfficeAutomation.Models;
using OfficeAutomation.Services.Tenancy;

namespace OfficeAutomation.Modules.Workflow.Infrastructure.Persistence;

public sealed class WorkflowDbContext : ModularDbContext, IWorkflowDbContext
{
    public WorkflowDbContext(
        DbContextOptions<WorkflowDbContext> options,
        ITenantIsolationService? tenantIsolationService = null)
        : base(options, tenantIsolationService)
    {
    }

    public DbSet<WorkflowRoute> WorkflowRoutes => Set<WorkflowRoute>();
    public DbSet<WorkflowDefinitionVersion> WorkflowDefinitionVersions => Set<WorkflowDefinitionVersion>();
    public DbSet<WorkflowStepDefinition> WorkflowStepDefinitions => Set<WorkflowStepDefinition>();
    public DbSet<WorkflowRule> WorkflowRules => Set<WorkflowRule>();
    public DbSet<WorkflowInstance> WorkflowInstances => Set<WorkflowInstance>();
    public DbSet<WorkflowStep> WorkflowSteps => Set<WorkflowStep>();
    public DbSet<WorkflowDecision> WorkflowDecisions => Set<WorkflowDecision>();
    public DbSet<WorkflowDelegation> WorkflowDelegations => Set<WorkflowDelegation>();
    public DbSet<WorkflowActionLog> WorkflowActionLogs => Set<WorkflowActionLog>();
    public DbSet<WorkflowAttachment> WorkflowAttachments => Set<WorkflowAttachment>();
    public DbSet<WorkflowComment> WorkflowComments => Set<WorkflowComment>();
    public DbSet<WorkflowEscalationEvent> WorkflowEscalationEvents => Set<WorkflowEscalationEvent>();
    public DbSet<WorkflowSlaJob> WorkflowSlaJobs => Set<WorkflowSlaJob>();
    public DbSet<WorkflowCaseTask> WorkflowCaseTasks => Set<WorkflowCaseTask>();
    public DbSet<WorkflowTransitionEvent> WorkflowTransitionEvents => Set<WorkflowTransitionEvent>();
    public DbSet<WorkflowIncident> WorkflowIncidents => Set<WorkflowIncident>();
    public DbSet<DocumentSignature> DocumentSignatures => Set<DocumentSignature>();
    public DbSet<ConnectorDeadLetterMessage> ConnectorDeadLetterMessages => Set<ConnectorDeadLetterMessage>();
    public DbSet<ConnectorExecutionLog> ConnectorExecutionLogs => Set<ConnectorExecutionLog>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<User> Users => Set<User>();
    public DbSet<ApplicationRole> Roles => Set<ApplicationRole>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();
    public DbSet<DocumentArchiveItem> DocumentArchiveItems => Set<DocumentArchiveItem>();
    public DbSet<Letter> Letters => Set<Letter>();
    public DbSet<Leave> Leaves => Set<Leave>();
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ApplyTenantSchema(modelBuilder);

        modelBuilder.Ignore<IdentityUserRole<string>>();
        modelBuilder.Ignore<IdentityUserClaim<string>>();
        modelBuilder.Ignore<IdentityUserLogin<string>>();
        modelBuilder.Ignore<IdentityUserToken<string>>();
        modelBuilder.Ignore<IdentityRoleClaim<string>>();

        modelBuilder.Entity<Department>(builder =>
        {
            builder.Ignore(item => item.Manager);
            builder.Ignore(item => item.ManagerEmployee);
            builder.Ignore(item => item.Users);
        });
        modelBuilder.Entity<User>(builder =>
        {
            builder.ToTable("AspNetUsers");
            builder.Ignore(item => item.Department);
            builder.Ignore(item => item.Manager);
            builder.Ignore(item => item.ParentManagerUser);
            builder.Ignore(item => item.Employee);
        });
        modelBuilder.Entity<ApplicationRole>().ToTable("AspNetRoles");

        modelBuilder.Entity<DocumentArchiveItem>(builder =>
        {
            builder.Property(item => item.HoldReason).HasMaxLength(1000);
            builder.HasIndex(item => item.IsUnderLegalHold);
        });

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(WorkflowDbContext).Assembly,
            type => type.Namespace?.StartsWith("OfficeAutomation.Modules.Workflow.", StringComparison.Ordinal) == true);
    }
}
