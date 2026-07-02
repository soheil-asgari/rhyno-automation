using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Modules.Identity.Infrastructure.Persistence;
using OfficeAutomation.Models;
using OfficeAutomation.Services.Security;

namespace OfficeAutomation.Controllers
{
    [ApiController]
    [Route("api/admin/roles-management")]
    [Authorize]
    [PermissionAuthorize("Security.Manage")]
    public class RolesManagementController : ControllerBase
    {
        private readonly IdentityDbContext _context;
        private readonly RoleManager<ApplicationRole> _roleManager;
        private readonly UserManager<User> _userManager;
        private readonly IPermissionAccessService _permissionAccessService;

        public RolesManagementController(
            IdentityDbContext context,
            RoleManager<ApplicationRole> roleManager,
            UserManager<User> userManager,
            IPermissionAccessService permissionAccessService)
        {
            _context = context;
            _roleManager = roleManager;
            _userManager = userManager;
            _permissionAccessService = permissionAccessService;
        }

        [HttpGet("overview")]
        public async Task<ActionResult<RoleManagementOverviewDto>> GetOverview(CancellationToken cancellationToken)
        {
            var permissions = await _context.Permissions
                .AsNoTracking()
                .OrderBy(item => item.Category)
                .ThenBy(item => item.Key)
                .Select(item => new PermissionDto
                {
                    Key = item.Key,
                    DisplayName = item.DisplayName,
                    Category = item.Category,
                    Description = item.Description
                })
                .ToListAsync(cancellationToken);

            var roles = await BuildRolesAsync(cancellationToken);
            var users = await BuildUsersAsync(cancellationToken);

            return Ok(new RoleManagementOverviewDto
            {
                Roles = roles,
                Permissions = permissions,
                Users = users,
                DataAccessScopes =
                [
                    new LookupItemDto { Value = RoleDataAccessScope.Department, Label = "فقط واحد مربوطه" },
                    new LookupItemDto { Value = RoleDataAccessScope.Global, Label = "سراسری" }
                ]
            });
        }

        [HttpGet("roles")]
        public async Task<ActionResult<IReadOnlyList<RoleManagementRoleDto>>> GetRoles(CancellationToken cancellationToken)
        {
            return Ok(await BuildRolesAsync(cancellationToken));
        }

        [HttpPost("roles")]
        public async Task<ActionResult<RoleManagementRoleDto>> CreateRole([FromBody] CreateRoleRequest request, CancellationToken cancellationToken)
        {
            if (!RoleDataAccessScope.All.Contains(request.DataAccessScope, StringComparer.OrdinalIgnoreCase))
            {
                return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]>
                {
                    [nameof(request.DataAccessScope)] = ["Unsupported data access scope."]
                }));
            }

            if (await _roleManager.RoleExistsAsync(request.Name.Trim()))
            {
                return Conflict(new { message = "Role name already exists." });
            }

            var role = new ApplicationRole
            {
                Name = request.Name.Trim(),
                Description = request.Description?.Trim(),
                DataAccessScope = request.DataAccessScope
            };

            var result = await _roleManager.CreateAsync(role);
            if (!result.Succeeded)
            {
                return BadRequest(new { message = string.Join(" | ", result.Errors.Select(item => item.Description)) });
            }

            var createdRole = await BuildRoleDtoAsync(role.Id, cancellationToken);
            return CreatedAtAction(nameof(GetRoles), new { id = role.Id }, createdRole);
        }

        [HttpPut("roles/{roleId}")]
        public async Task<ActionResult<RoleManagementRoleDto>> UpdateRole(string roleId, [FromBody] UpdateRoleRequest request, CancellationToken cancellationToken)
        {
            var role = await _roleManager.FindByIdAsync(roleId);
            if (role == null)
            {
                return NotFound();
            }

            if (!RoleDataAccessScope.All.Contains(request.DataAccessScope, StringComparer.OrdinalIgnoreCase))
            {
                return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]>
                {
                    [nameof(request.DataAccessScope)] = ["Unsupported data access scope."]
                }));
            }

            var normalizedName = request.Name.Trim();
            if (!string.Equals(role.Name, normalizedName, StringComparison.OrdinalIgnoreCase) &&
                await _roleManager.RoleExistsAsync(normalizedName))
            {
                return Conflict(new { message = "Role name already exists." });
            }

            role.Name = normalizedName;
            role.NormalizedName = normalizedName.ToUpperInvariant();
            role.Description = request.Description?.Trim();
            role.DataAccessScope = request.DataAccessScope;

            var result = await _roleManager.UpdateAsync(role);
            if (!result.Succeeded)
            {
                return BadRequest(new { message = string.Join(" | ", result.Errors.Select(item => item.Description)) });
            }

            var userIds = await _context.UserRoles
                .AsNoTracking()
                .Where(item => item.RoleId == roleId)
                .Select(item => item.UserId)
                .Distinct()
                .ToListAsync(cancellationToken);

            foreach (var userId in userIds)
            {
                await _permissionAccessService.InvalidateUserAsync(userId);
            }

            return Ok(await BuildRoleDtoAsync(roleId, cancellationToken));
        }

        [HttpDelete("roles/{roleId}")]
        public async Task<IActionResult> DeleteRole(string roleId, CancellationToken cancellationToken)
        {
            var role = await _roleManager.FindByIdAsync(roleId);
            if (role == null)
            {
                return NotFound();
            }

            var userCount = await _context.UserRoles.CountAsync(item => item.RoleId == roleId, cancellationToken);
            if (userCount > 0)
            {
                return Conflict(new { message = "Cannot delete a role that is assigned to users." });
            }

            _context.RolePermissions.RemoveRange(_context.RolePermissions.Where(item => item.RoleId == roleId));
            await _context.SaveChangesAsync(cancellationToken);

            var result = await _roleManager.DeleteAsync(role);
            if (!result.Succeeded)
            {
                return BadRequest(new { message = string.Join(" | ", result.Errors.Select(item => item.Description)) });
            }

            return NoContent();
        }

        [HttpPut("roles/{roleId}/permissions")]
        public async Task<ActionResult<RoleManagementRoleDto>> UpdateRolePermissions(
            string roleId,
            [FromBody] UpdateRolePermissionsRequest request,
            CancellationToken cancellationToken)
        {
            var roleExists = await _roleManager.Roles.AnyAsync(item => item.Id == roleId, cancellationToken);
            if (!roleExists)
            {
                return NotFound();
            }

            var requestedPermissions = request.PermissionKeys
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => item.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var validPermissions = await _context.Permissions
                .AsNoTracking()
                .Where(item => requestedPermissions.Contains(item.Key))
                .Select(item => item.Key)
                .ToListAsync(cancellationToken);

            if (validPermissions.Count != requestedPermissions.Count)
            {
                return BadRequest(new { message = "One or more permissions are invalid." });
            }

            var existingAssignments = await _context.RolePermissions
                .Where(item => item.RoleId == roleId)
                .ToListAsync(cancellationToken);

            _context.RolePermissions.RemoveRange(existingAssignments);
            await _context.SaveChangesAsync(cancellationToken);

            if (validPermissions.Count != 0)
            {
                _context.RolePermissions.AddRange(validPermissions.Select(item => new RolePermission
                {
                    RoleId = roleId,
                    PermissionKey = item,
                    IsAllowed = true
                }));
                await _context.SaveChangesAsync(cancellationToken);
            }

            var userIds = await _context.UserRoles
                .AsNoTracking()
                .Where(item => item.RoleId == roleId)
                .Select(item => item.UserId)
                .Distinct()
                .ToListAsync(cancellationToken);

            foreach (var userId in userIds)
            {
                await _permissionAccessService.InvalidateUserAsync(userId);
            }

            return Ok(await BuildRoleDtoAsync(roleId, cancellationToken));
        }

        [HttpGet("users")]
        public async Task<ActionResult<IReadOnlyList<RoleManagementUserDto>>> GetUsers(CancellationToken cancellationToken)
        {
            return Ok(await BuildUsersAsync(cancellationToken));
        }

        [HttpPut("users/{userId}/roles")]
        public async Task<ActionResult<RoleManagementUserDto>> UpdateUserRoles(
            string userId,
            [FromBody] UpdateUserRolesRequest request,
            CancellationToken cancellationToken)
        {
            var user = await _userManager.Users
                .FirstOrDefaultAsync(item => item.Id == userId, cancellationToken);
            if (user == null)
            {
                return NotFound();
            }

            var targetRoleIds = request.RoleIds
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => item.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var validRoles = await _roleManager.Roles
                .AsNoTracking()
                .Where(item => targetRoleIds.Contains(item.Id))
                .Select(item => new { item.Id, RoleName = item.Name! })
                .ToListAsync(cancellationToken);

            if (validRoles.Count != targetRoleIds.Count)
            {
                return BadRequest(new { message = "One or more roles are invalid." });
            }

            var currentRoles = await _userManager.GetRolesAsync(user);
            if (currentRoles.Count != 0)
            {
                var removeResult = await _userManager.RemoveFromRolesAsync(user, currentRoles);
                if (!removeResult.Succeeded)
                {
                    return BadRequest(new { message = string.Join(" | ", removeResult.Errors.Select(item => item.Description)) });
                }
            }

            if (validRoles.Count != 0)
            {
                var addResult = await _userManager.AddToRolesAsync(user, validRoles.Select(item => item.RoleName));
                if (!addResult.Succeeded)
                {
                    return BadRequest(new { message = string.Join(" | ", addResult.Errors.Select(item => item.Description)) });
                }
            }

            await _permissionAccessService.InvalidateUserAsync(userId);
            return Ok(await BuildUserDtoAsync(userId, cancellationToken));
        }

        [HttpGet("permissions")]
        public async Task<ActionResult<IReadOnlyList<PermissionDto>>> GetPermissions(CancellationToken cancellationToken)
        {
            var permissions = await _context.Permissions
                .AsNoTracking()
                .OrderBy(item => item.Category)
                .ThenBy(item => item.Key)
                .Select(item => new PermissionDto
                {
                    Key = item.Key,
                    DisplayName = item.DisplayName,
                    Category = item.Category,
                    Description = item.Description
                })
                .ToListAsync(cancellationToken);

            return Ok(permissions);
        }

        [HttpGet("me/access-profile")]
        public async Task<ActionResult<AccessProfileDto>> GetCurrentAccessProfile(CancellationToken cancellationToken)
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized();
            }

            var profile = await _permissionAccessService.GetAccessProfileAsync(userId, cancellationToken);
            if (profile == null)
            {
                return NotFound();
            }

            return Ok(new AccessProfileDto
            {
                UserId = profile.UserId,
                DisplayName = profile.DisplayName,
                DepartmentId = profile.DepartmentId,
                HasGlobalAccess = profile.HasGlobalAccess,
                Roles = profile.Roles,
                Permissions = profile.Permissions.OrderBy(item => item).ToList()
            });
        }

        private async Task<List<RoleManagementRoleDto>> BuildRolesAsync(CancellationToken cancellationToken)
        {
            var roles = await _roleManager.Roles
                .AsNoTracking()
                .OrderBy(item => item.Name)
                .ToListAsync(cancellationToken);

            var roleIds = roles.Select(item => item.Id).ToList();
            var permissions = roleIds.Count == 0
                ? []
                : await _context.RolePermissions
                    .AsNoTracking()
                    .Where(item => roleIds.Contains(item.RoleId) && item.IsAllowed)
                    .Select(item => new { item.RoleId, item.PermissionKey })
                    .ToListAsync(cancellationToken);

            var userCounts = roleIds.Count == 0
                ? new Dictionary<string, int>(StringComparer.Ordinal)
                : await _context.UserRoles
                    .AsNoTracking()
                    .Where(item => roleIds.Contains(item.RoleId))
                    .GroupBy(item => item.RoleId)
                    .Select(group => new { group.Key, Count = group.Count() })
                    .ToDictionaryAsync(item => item.Key, item => item.Count, cancellationToken);

            return roles.Select(role => new RoleManagementRoleDto
            {
                Id = role.Id,
                Name = role.Name ?? role.Id,
                Description = role.Description,
                DataAccessScope = role.DataAccessScope,
                Permissions = permissions
                    .Where(item => item.RoleId == role.Id)
                    .Select(item => item.PermissionKey)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(item => item)
                    .ToList(),
                UserCount = userCounts.TryGetValue(role.Id, out var userCount) ? userCount : 0
            }).ToList();
        }

        private async Task<RoleManagementRoleDto> BuildRoleDtoAsync(string roleId, CancellationToken cancellationToken)
        {
            var roles = await BuildRolesAsync(cancellationToken);
            return roles.First(item => item.Id == roleId);
        }

        private async Task<List<RoleManagementUserDto>> BuildUsersAsync(CancellationToken cancellationToken)
        {
            var users = await _context.Users
                .AsNoTracking()
                .Include(item => item.Department)
                .OrderBy(item => item.FullName ?? item.UserName)
                .ToListAsync(cancellationToken);

            var userIds = users.Select(item => item.Id).ToList();
            var rolePairs = userIds.Count == 0
                ? []
                : await (
                    from userRole in _context.UserRoles.AsNoTracking()
                    join role in _context.Roles.AsNoTracking() on userRole.RoleId equals role.Id
                    where userIds.Contains(userRole.UserId)
                    select new
                    {
                        userRole.UserId,
                        RoleName = role.Name ?? role.Id
                    })
                    .ToListAsync(cancellationToken);

            var permissionsByUser = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (var user in users)
            {
                var profile = await _permissionAccessService.GetAccessProfileAsync(user.Id, cancellationToken);
                permissionsByUser[user.Id] = profile?.Permissions.OrderBy(item => item).ToList() ?? [];
            }

            return users.Select(user => new RoleManagementUserDto
            {
                Id = user.Id,
                DisplayName = string.IsNullOrWhiteSpace(user.FullName) ? (user.UserName ?? user.Email ?? user.Id) : user.FullName,
                Email = user.Email,
                DepartmentId = user.DepartmentId,
                DepartmentName = user.Department?.Name,
                Roles = rolePairs
                    .Where(item => item.UserId == user.Id)
                    .Select(item => item.RoleName)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(item => item)
                    .ToList(),
                Permissions = permissionsByUser[user.Id]
            }).ToList();
        }

        private async Task<RoleManagementUserDto> BuildUserDtoAsync(string userId, CancellationToken cancellationToken)
        {
            var users = await BuildUsersAsync(cancellationToken);
            return users.First(item => item.Id == userId);
        }
    }
}
