namespace OfficeAutomation.Models
{
    /// <summary>
    /// Represents a single breadcrumb item for the reusable breadcrumb component.
    /// </summary>
    public class RhynoBreadcrumbItem
    {
        public string Label { get; set; } = string.Empty;
        public string? Url { get; set; }

        public RhynoBreadcrumbItem() { }

        public RhynoBreadcrumbItem(string label, string? url = null)
        {
            Label = label;
            Url = url;
        }
    }
}