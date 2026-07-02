using OfficeAutomation.Models;
using OfficeAutomation.Modules.Workflow.Infrastructure.Persistence;

namespace OfficeAutomation.Services.Outbox;

public interface IOutboxService
{
    void EnqueueWorkflowStatusChanged(IWorkflowDbContext context, WorkflowStatusChangedIntegrationEvent integrationEvent);
    void EnqueueWorkflowEscalated(IWorkflowDbContext context, WorkflowEscalatedIntegrationEvent integrationEvent);
}
