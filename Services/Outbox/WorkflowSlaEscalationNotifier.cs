using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Models;
using OfficeAutomation.Modules.Workflow.Infrastructure.Persistence;

namespace OfficeAutomation.Services;

public sealed class WorkflowSlaEscalationNotifier
{
    private readonly IWorkflowDbContext _context;
    private readonly NotificationService _notificationService;
    private readonly Services.Outbox.IOutboxService? _outboxService;

    public WorkflowSlaEscalationNotifier(
        IWorkflowDbContext context,
        NotificationService notificationService,
        Services.Outbox.IOutboxService? outboxService = null)
    {
        _context = context;
        _notificationService = notificationService;
        _outboxService = outboxService;
    }

    public async Task<bool> EscalateStepAsync(int workflowStepId, CancellationToken cancellationToken = default)
    {
        var step = await _context.WorkflowSteps
            .Include(item => item.WorkflowInstance)
            .FirstOrDefaultAsync(item => item.Id == workflowStepId, cancellationToken);
        if (step?.WorkflowInstance == null || step.CompletedAt != null)
        {
            return false;
        }

        var currentState = step.SlaState ?? WorkflowSlaState.OnTrack;
        if (string.Equals(currentState, WorkflowSlaState.Breached, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        step.SlaState = WorkflowSlaState.Breached;
        step.WorkflowInstance.SlaState = WorkflowSlaState.Breached;
        step.EscalatedAt ??= DateTimeOffset.UtcNow;

        _context.WorkflowEscalationEvents.Add(new WorkflowEscalationEvent
        {
            WorkflowInstanceId = step.WorkflowInstanceId,
            WorkflowStepId = step.Id,
            EscalatedToUserId = step.DelegatedFromUserId ?? step.AssignedToUserId,
            EscalatedToRoleId = step.AssignedRoleId,
            PreviousSlaState = currentState,
            NewSlaState = WorkflowSlaState.Breached,
            Note = "SLA breach triggered automatically by scheduled job.",
            EscalatedAt = DateTimeOffset.UtcNow
        });

        var recipientUserId = step.DelegatedFromUserId ?? step.AssignedToUserId ?? step.WorkflowInstance.StartedByUserId;
        if (!string.IsNullOrWhiteSpace(recipientUserId))
        {
            await _notificationService.UpsertActiveAsync(
                recipientUserId,
                "Workflow SLA breached",
                $"Step {step.StepNumber} for {step.WorkflowInstance.DocumentType} #{step.WorkflowInstance.DocumentId} breached its SLA.",
                NotificationSeverity.Warning,
                $"/{GetModulePath(step.WorkflowInstance.DocumentType)}/Details/{step.WorkflowInstance.DocumentId}",
                "Workflow",
                "WorkflowEscalation",
                step.WorkflowInstance.DocumentId,
                expiresAt: DateTimeOffset.UtcNow.AddDays(7),
                cancellationToken: cancellationToken);
        }

        _outboxService?.EnqueueWorkflowEscalated(_context, new WorkflowEscalatedIntegrationEvent
        {
            DocumentType = step.WorkflowInstance.DocumentType,
            DocumentId = step.WorkflowInstance.DocumentId,
            WorkflowInstanceId = step.WorkflowInstanceId,
            WorkflowStepId = step.Id,
            StepNumber = step.StepNumber,
            PreviousSlaState = currentState,
            NewSlaState = WorkflowSlaState.Breached,
            EscalatedToUserId = step.DelegatedFromUserId ?? step.AssignedToUserId,
            EscalatedToRoleId = step.AssignedRoleId,
            CorrelationId = Activity.Current?.Id
        });

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static string GetModulePath(string documentType) => documentType switch
    {
        "Letter" => "Letters",
        "Invoice" => "Financial",
        "InventoryTransferRequest" => "Warehouse",
        _ => documentType
    };
}
