namespace OfficeAutomation.Services.Security
{
    public sealed class PermissionAccessProfile
    {
        public string UserId { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public int? DepartmentId { get; init; }
        public bool HasGlobalAccess { get; init; }
        public string Clearance { get; init; } = "Standard";
        public IReadOnlyList<string> Roles { get; init; } = [];
        public IReadOnlySet<string> Permissions { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public IReadOnlyDictionary<string, string?> Attributes { get; init; } = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
    }
}
