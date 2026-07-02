namespace OfficeAutomation.Models;

public sealed class WorkflowTimeInStateMetric
{
    public string StationKey { get; init; } = string.Empty;
    public string StationName { get; init; } = string.Empty;
    public double AverageHours { get; init; }
    public int SampleCount { get; init; }
}

public sealed class WorkflowReworkLoopMetric
{
    public string StationKey { get; init; } = string.Empty;
    public int LoopCount { get; init; }
}

public sealed class WorkflowBottleneckMetric
{
    public string StationKey { get; init; } = string.Empty;
    public string StationName { get; init; } = string.Empty;
    public double TotalHours { get; init; }
    public int VisitCount { get; init; }
}
