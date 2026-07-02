# Modular Monolith Boundaries

`ApplicationDbContext` is now a legacy facade. New code must depend on the owning module context or on an application contract exposed by the owner module.

## Context Ownership

- `Modules.Identity.Infrastructure.Persistence.IdentityDbContext`
  owns users, roles, departments, permissions, role permissions, role conflict rules, and user preferences.
- `Modules.Workflow.Infrastructure.Persistence.WorkflowDbContext`
  owns workflow definitions, instances, steps, decisions, delegations, workflow logs, SLA jobs, connector logs, process-mining events, and outbox messages.
- `Modules.Finance.Infrastructure.Persistence.FinanceDbContext`
  owns invoices, invoice items, waybills, vendors, employers, insurance, and payroll.

## Cross-Cutting Persistence

Common persistence behavior is centralized under `Data/`:

- `ITenantSchemaDbContext` exposes the current tenant schema to EF model-cache keys.
- `IAuditableDbContext` gives `AuditSaveChangesInterceptor` a module-agnostic audit surface.
- `ModularDbContext` is the base for non-Identity module contexts.
- `AuditEntryFactory` contains the audit-entry extraction logic that used to live inside `ApplicationDbContext`.

## Cross-Module Communication

Do not join another module's tables from a controller or service.

Finance-to-Workflow write flow:

```csharp
public sealed record InvoiceSubmittedForApproval(
    int InvoiceId,
    string SubmittedByUserId,
    decimal Amount,
    DateTimeOffset OccurredAt);
```

Finance publishes the event after saving the invoice. Workflow handles it and creates the workflow instance using `WorkflowDbContext`.

Finance-to-Workflow read flow:

```csharp
public interface IWorkflowTaskQuery
{
    Task<IReadOnlyList<UserTaskDto>> GetPendingTasksAsync(
        string userId,
        CancellationToken cancellationToken = default);
}
```

The implementation belongs to Workflow and can use `WorkflowDbContext` or a workflow read model. Finance consumes only the interface.

## Read Models

Dashboards and inboxes should read from module-owned snapshots such as:

- `WorkflowInboxItemReadModel`
- `FinanceDashboardSnapshot`
- `ProcessMiningSnapshot`

These tables are updated by domain-event handlers or outbox processors, not by heavy real-time `Include` graphs in MVC actions.
