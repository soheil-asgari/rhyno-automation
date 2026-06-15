namespace OfficeAutomation.Models
{
    public class GlobalSearchViewModel
    {
        public string? Query { get; set; }

        public List<GlobalSearchResultViewModel> Results { get; set; } = new();

        public int TotalCount => Results.Count;
    }

    public class GlobalSearchResultViewModel
    {
        public string Module { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public string? Subtitle { get; set; }

        public string Url { get; set; } = "#";

        public string Icon { get; set; } = "bi-search";

        public string Tone { get; set; } = "secondary";
    }
}
