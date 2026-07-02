using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Models;
using OfficeAutomation.Modules.Workflow.Infrastructure.Persistence;

namespace OfficeAutomation.Services;

public sealed class ProcessMiningService
{
    private readonly IWorkflowDbContext _context;

    public ProcessMiningService(IWorkflowDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<WorkflowTimeInStateMetric>> GetAverageTimeInStateAsync(string documentType, CancellationToken cancellationToken = default)
    {
        var events = await _context.WorkflowTransitionEvents
            .AsNoTracking()
            .Where(item => item.WorkflowInstance != null && item.WorkflowInstance.DocumentType == documentType)
            .OrderBy(item => item.WorkflowInstanceId)
            .ThenBy(item => item.SequenceNumber)
            .Select(item => new
            {
                item.WorkflowInstanceId,
                item.StationKey,
                item.StationName,
                item.OccurredAt
            })
            .ToListAsync(cancellationToken);

        var metrics = events
            .GroupBy(item => item.WorkflowInstanceId)
            .SelectMany(group => group.Zip(group.Skip(1), (from, to) => new
            {
                from.StationKey,
                from.StationName,
                Hours = (to.OccurredAt - from.OccurredAt).TotalHours
            }))
            .Where(item => !string.IsNullOrWhiteSpace(item.StationKey) && item.Hours >= 0)
            .GroupBy(item => new { item.StationKey, item.StationName })
            .Select(group => new WorkflowTimeInStateMetric
            {
                StationKey = group.Key.StationKey!,
                StationName = group.Key.StationName ?? group.Key.StationKey!,
                AverageHours = group.Average(item => item.Hours),
                SampleCount = group.Count()
            })
            .OrderByDescending(item => item.AverageHours)
            .ToList();

        return metrics;
    }

    public async Task<IReadOnlyList<WorkflowReworkLoopMetric>> DetectReworkLoopsAsync(string documentType, CancellationToken cancellationToken = default)
    {
        var events = await _context.WorkflowTransitionEvents
            .AsNoTracking()
            .Where(item => item.WorkflowInstance != null && item.WorkflowInstance.DocumentType == documentType && item.StationKey != null)
            .OrderBy(item => item.WorkflowInstanceId)
            .ThenBy(item => item.SequenceNumber)
            .Select(item => new { item.WorkflowInstanceId, item.StationKey })
            .ToListAsync(cancellationToken);

        return events
            .GroupBy(item => item.WorkflowInstanceId)
            .SelectMany(group => group.GroupBy(item => item.StationKey!).Where(x => x.Count() > 1).Select(x => new { x.Key, LoopCount = x.Count() - 1 }))
            .GroupBy(item => item.Key)
            .Select(group => new WorkflowReworkLoopMetric
            {
                StationKey = group.Key,
                LoopCount = group.Sum(item => item.LoopCount)
            })
            .OrderByDescending(item => item.LoopCount)
            .ToList();
    }

    public async Task<IReadOnlyList<WorkflowBottleneckMetric>> GetBottlenecksAsync(string documentType, CancellationToken cancellationToken = default)
    {
        var averages = await GetAverageTimeInStateAsync(documentType, cancellationToken);
        return averages
            .Select(item => new WorkflowBottleneckMetric
            {
                StationKey = item.StationKey,
                StationName = item.StationName,
                TotalHours = item.AverageHours * item.SampleCount,
                VisitCount = item.SampleCount
            })
            .OrderByDescending(item => item.TotalHours)
            .ToList();
    }
}
