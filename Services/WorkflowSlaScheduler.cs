using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Models;
using OfficeAutomation.Modules.Workflow.Infrastructure.Persistence;

namespace OfficeAutomation.Services;

public sealed class WorkflowSlaScheduler
{
    private static readonly TimeSpan BusinessDayStart = TimeSpan.FromHours(8);
    private static readonly TimeSpan BusinessDayEnd = TimeSpan.FromHours(16);

    private readonly IWorkflowDbContext _context;
    private readonly Tenancy.ITenantSettingsService? _tenantSettingsService;

    public WorkflowSlaScheduler(IWorkflowDbContext context, Tenancy.ITenantSettingsService? tenantSettingsService = null)
    {
        _context = context;
        _tenantSettingsService = tenantSettingsService;
    }

    public async Task<DateTimeOffset?> ScheduleStepAsync(
        WorkflowInstance instance,
        WorkflowStep step,
        int slaHours,
        CancellationToken cancellationToken = default)
    {
        await CancelStepJobsAsync(step.Id, "Superseded by a new SLA schedule.", cancellationToken);

        var dueAt = await CalculateDueAtAsync(DateTimeOffset.UtcNow, slaHours, cancellationToken);
        step.DueAt = dueAt;
        step.SlaState = WorkflowSlaState.OnTrack;
        instance.DueAt = dueAt;
        instance.SlaState = WorkflowSlaState.OnTrack;

        _context.WorkflowSlaJobs.Add(new WorkflowSlaJob
        {
            WorkflowInstanceId = instance.Id,
            WorkflowStepId = step.Id,
            ScheduledFor = dueAt
        });

        return dueAt;
    }

    public async Task CancelStepJobsAsync(int workflowStepId, string reason, CancellationToken cancellationToken = default)
    {
        var jobs = await _context.WorkflowSlaJobs
            .Where(item => item.WorkflowStepId == workflowStepId && item.Status == WorkflowSlaJobStatus.Scheduled)
            .ToListAsync(cancellationToken);

        if (jobs.Count == 0)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        foreach (var job in jobs)
        {
            job.Status = WorkflowSlaJobStatus.Canceled;
            job.CanceledAt = now;
            job.CancellationReason = reason;
        }
    }

    public async Task<DateTimeOffset> CalculateDueAtAsync(DateTimeOffset baselineUtc, int slaHours, CancellationToken cancellationToken = default)
    {
        var settings = _tenantSettingsService == null
            ? await _context.SystemSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken)
            : await _tenantSettingsService.GetSystemSettingsAsync(cancellationToken);
        var timeZoneId = settings?.TimeZoneId ?? "Asia/Tehran";
        TimeZoneInfo zone;
        try
        {
            zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch
        {
            zone = TimeZoneInfo.Utc;
        }

        var remaining = Math.Max(1, slaHours);
        var local = TimeZoneInfo.ConvertTime(baselineUtc, zone);
        local = MoveToBusinessTime(local, zone);

        while (remaining > 0)
        {
            if (!IsBusinessDay(local.Date))
            {
                local = MoveToNextBusinessDay(local.Date, zone);
                continue;
            }

            var businessEnd = local.Date + BusinessDayEnd;
            var available = businessEnd - local.DateTime;
            if (available.TotalHours <= 0)
            {
                local = MoveToNextBusinessDay(local.Date.AddDays(1), zone);
                continue;
            }

            var consume = Math.Min(remaining, (int)Math.Ceiling(available.TotalHours));
            local = local.AddHours(consume);
            remaining -= consume;

            if (remaining > 0)
            {
                local = MoveToNextBusinessDay(local.Date.AddDays(1), zone);
            }
        }

        return TimeZoneInfo.ConvertTime(local, TimeZoneInfo.Utc);
    }

    private static DateTimeOffset MoveToBusinessTime(DateTimeOffset local, TimeZoneInfo zone)
    {
        if (!IsBusinessDay(local.Date))
        {
            return MoveToNextBusinessDay(local.Date, zone);
        }

        var time = local.TimeOfDay;
        if (time < BusinessDayStart)
        {
            return new DateTimeOffset(local.Date + BusinessDayStart, local.Offset);
        }

        if (time >= BusinessDayEnd)
        {
            return MoveToNextBusinessDay(local.Date.AddDays(1), zone);
        }

        return local;
    }

    private static DateTimeOffset MoveToNextBusinessDay(DateTime date, TimeZoneInfo zone)
    {
        var next = date;
        while (!IsBusinessDay(next))
        {
            next = next.AddDays(1);
        }

        var unspecified = next + BusinessDayStart;
        return new DateTimeOffset(unspecified, zone.GetUtcOffset(unspecified));
    }

    private static bool IsBusinessDay(DateTime date)
    {
        return date.DayOfWeek is not DayOfWeek.Thursday and not DayOfWeek.Friday;
    }
}
