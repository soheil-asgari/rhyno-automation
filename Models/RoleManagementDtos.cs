using System.ComponentModel.DataAnnotations;

namespace OfficeAutomation.Models
{
    public sealed class RoleManagementRoleDto
    {
        public string Id { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string? Description { get; init; }
        public string DataAccessScope { get; init; } = RoleDataAccessScope.Department;
        public IReadOnlyList<string> Permissions { get; init; } = [];
        public int UserCount { get; init; }
    }

    public sealed class PermissionDto
    {
        public string Key { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public string Category { get; init; } = string.Empty;
        public string? Description { get; init; }
    }

    public sealed class RoleManagementUserDto
    {
        public string Id { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public string? Email { get; init; }
        public int? DepartmentId { get; init; }
        public string? DepartmentName { get; init; }
        public IReadOnlyList<string> Roles { get; init; } = [];
        public IReadOnlyList<string> Permissions { get; init; } = [];
    }

    public sealed class RoleManagementOverviewDto
    {
        public IReadOnlyList<RoleManagementRoleDto> Roles { get; init; } = [];
        public IReadOnlyList<PermissionDto> Permissions { get; init; } = [];
        public IReadOnlyList<RoleManagementUserDto> Users { get; init; } = [];
        public IReadOnlyList<LookupItemDto> DataAccessScopes { get; init; } = [];
    }

    public sealed class LookupItemDto
    {
        public string Value { get; init; } = string.Empty;
        public string Label { get; init; } = string.Empty;
    }

    public sealed class CreateRoleRequest
    {
        [Required]
        [StringLength(256)]
        public string Name { get; init; } = string.Empty;

        [StringLength(256)]
        public string? Description { get; init; }

        [Required]
        [StringLength(32)]
        public string DataAccessScope { get; init; } = RoleDataAccessScope.Department;
    }

    public sealed class UpdateRoleRequest
    {
        [Required]
        [StringLength(256)]
        public string Name { get; init; } = string.Empty;

        [StringLength(256)]
        public string? Description { get; init; }

        [Required]
        [StringLength(32)]
        public string DataAccessScope { get; init; } = RoleDataAccessScope.Department;
    }

    public sealed class UpdateRolePermissionsRequest
    {
        [Required]
        public IReadOnlyList<string> PermissionKeys { get; init; } = [];
    }

    public sealed class UpdateUserRolesRequest
    {
        [Required]
        public IReadOnlyList<string> RoleIds { get; init; } = [];
    }

    public sealed class AccessProfileDto
    {
        public string UserId { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public int? DepartmentId { get; init; }
        public bool HasGlobalAccess { get; init; }
        public IReadOnlyList<string> Roles { get; init; } = [];
        public IReadOnlyList<string> Permissions { get; init; } = [];
    }
}
