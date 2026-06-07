namespace OfficeAutomation.Services.Security
{
    public sealed class PermissionAccessProfile
    {
        public string UserId { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public int? DepartmentId { get; init; }
        public bool HasGlobalAccess { get; init; }
        public IReadOnlyList<string> Roles { get; init; } = [];
        public IReadOnlySet<string> Permissions { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }
}
