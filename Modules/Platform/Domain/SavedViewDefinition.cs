namespace OfficeAutomation.Modules.Platform.Domain;

public sealed class SavedViewDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string TargetGridId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string ColumnLayoutJson { get; set; } = "[]";
    public string FilterQueryJson { get; set; } = "{}";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
