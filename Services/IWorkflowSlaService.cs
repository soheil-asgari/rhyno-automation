using OfficeAutomation.Models;

namespace OfficeAutomation.Services;

public interface IWorkflowSlaService
{
    Task ScheduleStepAsync(WorkflowInstance instance, WorkflowStep step, int slaHours, CancellationToken cancellationToken = default);
    Task CancelStepJobsAsync(int stepId, string reason, CancellationToken cancellationToken = default);
}

public sealed class WorkflowSlaService : IWorkflowSlaService
{
    private readonly WorkflowSlaScheduler? _slaScheduler;

    public WorkflowSlaService(WorkflowSlaScheduler? slaScheduler = null)
    {
        _slaScheduler = slaScheduler;
    }

    public async Task ScheduleStepAsync(WorkflowInstance instance, WorkflowStep step, int slaHours, CancellationToken cancellationToken = default)
    {
        if (_slaScheduler != null)
        {
            await _slaScheduler.ScheduleStepAsync(instance, step, slaHours, cancellationToken);
        }
    }

    public async Task CancelStepJobsAsync(int stepId, string reason, CancellationToken cancellationToken = default)
    {
        if (_slaScheduler != null)
        {
            await _slaScheduler.CancelStepJobsAsync(stepId, reason, cancellationToken);
        }
    }
}