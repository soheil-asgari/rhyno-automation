using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Models;
using OfficeAutomation.Modules.Workflow.Infrastructure.Persistence;
using OfficeAutomation.Services.Outbox;
using Stateless;
using Microsoft.AspNetCore.SignalR;

namespace OfficeAutomation.Services
{
    public interface IWorkflowExecutionService
    {
        Task ExecuteNextStepAsync(int workflowInstanceId, string triggerAction, CancellationToken cancellationToken = default);
    }

    public sealed class WorkflowExecutionService : IWorkflowExecutionService
    {
        private readonly IWorkflowDbContext _context;
        private readonly IOutboxService _outboxService;
        private readonly Microsoft.AspNetCore.SignalR.IHubContext<OfficeAutomation.Hubs.AutomationHub> _hubContext;

        public WorkflowExecutionService(
            IWorkflowDbContext context, 
            IOutboxService outboxService,
            Microsoft.AspNetCore.SignalR.IHubContext<OfficeAutomation.Hubs.AutomationHub> hubContext)
        {
            _context = context;
            _outboxService = outboxService;
            _hubContext = hubContext;
        }

        public async Task ExecuteNextStepAsync(int workflowInstanceId, string triggerAction, CancellationToken cancellationToken = default)
        {
            // 1. Fetch WorkflowInstance and associated definitions
            var instance = await _context.WorkflowInstances
                .Include(i => i.Steps)
                .Include(i => i.DefinitionVersion)
                .FirstOrDefaultAsync(i => i.Id == workflowInstanceId, cancellationToken);

            if (instance == null)
            {
                throw new ArgumentException($"Workflow instance with ID {workflowInstanceId} not found.", nameof(workflowInstanceId));
            }

            var docType = instance.DocumentType;

            // Fetch active routes (transitions) for this DocumentType
            var routes = await _context.WorkflowRoutes
                .AsNoTracking()
                .Where(r => r.DocumentType == docType && r.IsActive)
                .OrderBy(r => r.StepNumber)
                .ToListAsync(cancellationToken);

            if (!routes.Any())
            {
                throw new InvalidOperationException($"No active workflow routes defined for document type: {docType}");
            }

            // Current active step
            var currentStep = instance.Steps
                .FirstOrDefault(s => s.Status == WorkflowStatus.PendingApproval);

            int currentStepId = currentStep?.Id ?? 0; // 0 represents the entry/Start state if no step is active

            // 2. Instantiate StateMachine using Stateless
            // State: int (WorkflowStep.Id or 0 for start)
            // Trigger: string (triggerAction)
            var stateMachine = new StateMachine<int, string>(currentStepId);

            // 3. Configure transitions using WorkflowRoutes
            // We model steps sequentially or custom-routed.
            // A transition flows from Route.StepNumber - 1 to Route.StepNumber.
            // If currentStepId is 0 (start), the first transition (StepNumber = 1) is permitted.
            
            // Map routes to step IDs or step numbers
            for (int i = 0; i < routes.Count; i++)
            {
                var route = routes[i];
                var sourceState = i == 0 ? 0 : (instance.Steps.FirstOrDefault(s => s.StepNumber == i)?.Id ?? 0);
                
                // Target step is the step at current index. We retrieve it or create a new state key.
                var targetStep = instance.Steps.FirstOrDefault(s => s.StepNumber == route.StepNumber);
                var targetState = targetStep?.Id ?? (10000 + route.StepNumber); // safe fallback key

                var config = stateMachine.Configure(sourceState);

                // Use triggerAction to invoke next step transition
                if (triggerAction.Equals("Approve", StringComparison.OrdinalIgnoreCase) ||
                    triggerAction.Equals(route.StepName, StringComparison.OrdinalIgnoreCase))
                {
                    config.Permit(triggerAction, targetState);
                }
            }

            // 4. Configure entry actions for new states
            foreach (var route in routes)
            {
                var step = instance.Steps.FirstOrDefault(s => s.StepNumber == route.StepNumber);
                if (step != null)
                {
                    stateMachine.Configure(step.Id)
                        .OnEntryAsync(async () =>
                        {
                            // A. Generate WorkflowCaseTask for the assigned user/role of that step
                            var task = new WorkflowCaseTask
                            {
                                WorkflowInstanceId = instance.Id,
                                WorkflowStepId = step.Id,
                                TaskType = WorkflowCaseTaskType.AdHoc,
                                Status = WorkflowCaseTaskStatus.Pending,
                                Title = $"تایید مرحله: {step.StepName}",
                                Description = $"درخواست بررسی و تایید گام {step.StepNumber} برای سند {instance.DocumentType}",
                                CreatedByUserId = instance.StartedByUserId ?? string.Empty,
                                AssignedToUserId = step.AssignedToUserId,
                                CreatedAt = DateTimeOffset.UtcNow
                            };
                            _context.WorkflowCaseTasks.Add(task);

                            // B. Create Status Changed event
                            var statusEvent = new WorkflowStatusChangedIntegrationEvent
                            {
                                DocumentType = instance.DocumentType,
                                DocumentId = instance.DocumentId,
                                WorkflowInstanceId = instance.Id,
                                WorkflowStepId = step.Id,
                                StepNumber = step.StepNumber,
                                DecisionType = triggerAction,
                                PreviousStatus = instance.Status,
                                CurrentStatus = step.Status,
                                CurrentWorkflowStatus = step.Status,
                                CurrentAssigneeUserId = step.AssignedToUserId,
                                CurrentAssigneeRoleId = step.AssignedRoleId,
                                CurrentAssigneeDepartmentId = step.AssignedDepartmentId,
                                ActorUserId = instance.StartedByUserId ?? string.Empty,
                                OccurredAt = DateTimeOffset.UtcNow
                            };

                            // C. Enqueue event in outbox
                            _outboxService.EnqueueWorkflowStatusChanged(_context, statusEvent);

                            // D. Send Real-Time SignalR Notification
                            if (!string.IsNullOrWhiteSpace(step.AssignedToUserId))
                            {
                                try
                                {
                                    await _hubContext.Clients.User(step.AssignedToUserId).SendAsync(
                                        "ReceiveNewTaskNotification",
                                        new
                                        {
                                            TaskId = task.Id,
                                            Title = "کار جدید",
                                            Message = $"یک کار جدید در کارتابل به شما ارجاع داده شد: {task.Title}"
                                        }
                                    );
                                }
                                catch { }
                            }
                        });
                }
            }

            // 5. Fire transition
            if (stateMachine.CanFire(triggerAction))
            {
                await stateMachine.FireAsync(triggerAction);
                
                // Update instance status properties
                var nextStateId = stateMachine.State;
                var nextStep = instance.Steps.FirstOrDefault(s => s.Id == nextStateId);
                if (nextStep != null)
                {
                    instance.CurrentStepNumber = nextStep.StepNumber;
                    instance.CurrentAssigneeUserId = nextStep.AssignedToUserId;
                    instance.CurrentAssigneeRoleId = nextStep.AssignedRoleId;
                    instance.CurrentAssigneeDepartmentId = nextStep.AssignedDepartmentId;
                    instance.Status = nextStep.Status;
                    instance.CurrentStatus = nextStep.Status;
                }
                else if (nextStateId >= 10000)
                {
                    // Newly transitioned route step numbers
                    instance.CurrentStepNumber = nextStateId - 10000;
                }

                await _context.SaveChangesAsync(cancellationToken);
            }
            else
            {
                throw new InvalidOperationException($"Transition '{triggerAction}' is not allowed from the current state.");
            }
        }
    }
}
