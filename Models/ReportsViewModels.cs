namespace OfficeAutomation.Models
{
    public class ReportsIndexViewModel
    {
        public List<ReportModuleViewModel> Modules { get; set; } = new();

        public List<ReportSectionViewModel> Sections { get; set; } = new();

        public List<ReportSummaryCardViewModel> SummaryCards { get; set; } = new();

        public List<ReportPresetViewModel> FilterPresets { get; set; } = new();
    }

    public class ReportModuleViewModel
    {
        public string Title { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string Icon { get; set; } = "bi-file-earmark-spreadsheet";

        public string Tone { get; set; } = "primary";

        public List<ReportActionViewModel> Actions { get; set; } = new();
    }

    public class ReportSectionViewModel
    {
        public string Title { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string Tone { get; set; } = "primary";

        public List<ReportModuleViewModel> Modules { get; set; } = new();
    }

    public class ReportSummaryCardViewModel
    {
        public string Title { get; set; } = string.Empty;

        public string Value { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string Tone { get; set; } = "primary";
    }

    public class ReportPresetViewModel
    {
        public string Label { get; set; } = string.Empty;

        public string Url { get; set; } = "#";

        public string Kind { get; set; } = "filter";
    }

    public class ReportActionViewModel
    {
        public string Label { get; set; } = string.Empty;

        public string Url { get; set; } = "#";

        public string Kind { get; set; } = "Excel";
    }
}
