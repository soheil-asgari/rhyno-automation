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
        public int TodayWorkItems { get; init; }
        public int OpenInvoices { get; init; }
        public int CriticalStockItems { get; init; }
        public int ImportantLetters { get; init; }
        public int SystemWarnings { get; init; }
        public IReadOnlyList<DashboardMetricCard> ExecutiveMetrics { get; init; } = [];
        public IReadOnlyList<DashboardWorkItem> PendingWorkItems { get; init; } = [];
        public IReadOnlyList<DashboardSystemAlert> SystemAlerts { get; init; } = [];
        public IReadOnlyList<DashboardChartPoint> WeeklyActivity { get; init; } = [];
        public IReadOnlyList<DashboardRecentActivity> RecentActivities { get; init; } = [];
    }

    public sealed class DashboardMetricCard
    {
        public string Title { get; init; } = string.Empty;
        public string Value { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public string Icon { get; init; } = "bi-speedometer2";
        public string Tone { get; init; } = "primary";
        public string Url { get; init; } = "#";
    }

    public sealed class DashboardWorkItem
    {
        public string Title { get; init; } = string.Empty;
        public string Module { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
        public string StatusCssClass { get; init; } = "text-bg-secondary";
        public string DateText { get; init; } = string.Empty;
        public string Url { get; init; } = "#";
        public string Icon { get; init; } = "bi-circle";
    }

    public sealed class DashboardSystemAlert
    {
        public string Title { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public string Severity { get; init; } = "info";
        public string Url { get; init; } = "#";
        public string Icon { get; init; } = "bi-info-circle";
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
