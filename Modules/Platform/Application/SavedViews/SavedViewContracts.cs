namespace OfficeAutomation.Modules.Platform.Application.SavedViews;

public sealed record SavedViewColumnLayout(
    string ColumnId,
    int Order,
    int Width,
    bool IsVisible);

public sealed record SavedViewColumnDefinition(
    string ColumnId,
    string DisplayName,
    string PropertyPath,
    IReadOnlyCollection<string> RequiredRoles);

public sealed class SavedViewFilterNode
{
    public string? Logic { get; set; }
    public List<SavedViewFilterNode>? Filters { get; set; }
    public string? Field { get; set; }
    public string? Operator { get; set; }
    public object? Value { get; set; }
}
