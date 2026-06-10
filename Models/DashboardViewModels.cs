namespace OfficeAutomation.Models
{
    public sealed class DashboardIndexViewModel
    {
        public string TodayText { get; init; } = string.Empty;
        public int TotalAuditEvents { get; init; }
        public int CreateEvents { get; init; }
        public int UpdateEvents { get; init; }
        public int DeleteEvents { get; init; }
        public int NewLetters { get; init; }
        public int PendingLeaves { get; init; }
        public int SentLetters { get; init; }
        public int TotalUsers { get; init; }
        public int WorkloadPercent { get; init; }
        public IReadOnlyList<DashboardChartPoint> WeeklyActivity { get; init; } = [];
        public IReadOnlyList<DashboardRecentActivity> RecentActivities { get; init; } = [];
    }

    public sealed class DashboardChartPoint
    {
        public string Label { get; init; } = string.Empty;
        public int Value { get; init; }
        public int Percent { get; init; }
    }

    public sealed class DashboardRecentActivity
    {
        public string DocumentType { get; init; } = string.Empty;
        public string DateText { get; init; } = string.Empty;
        public string StatusText { get; init; } = string.Empty;
        public string StatusCssClass { get; init; } = "pending";
    }
}
