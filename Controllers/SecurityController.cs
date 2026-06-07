using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Data;
using OfficeAutomation.Filters;
using OfficeAutomation.Models;
using OfficeAutomation.Services.Security;

namespace OfficeAutomation.Controllers
{
    [Authorize]
    [PermissionAuthorize("Security.Manage")]
    public class SecurityController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly RoleManager<ApplicationRole> _roleManager;

        public SecurityController(ApplicationDbContext context, RoleManager<ApplicationRole> roleManager)
        {
            _context = context;
            _roleManager = roleManager;
        }

        [HttpGet]
        public async Task<IActionResult> Index(CancellationToken cancellationToken)
        {
            var roles = await _roleManager.Roles.OrderBy(item => item.Name).ToListAsync(cancellationToken);
            return View(roles);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateRole(SecurityRoleEditVM model, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return RedirectToAction(nameof(Index));
            }

            var roleName = model.Name.Trim();
            if (await _roleManager.RoleExistsAsync(roleName))
            {
                TempData["SecurityMessage"] = "نقش وارد شده تکراری است.";
                return RedirectToAction(nameof(Index));
            }

            var result = await _roleManager.CreateAsync(new ApplicationRole
            {
                Name = roleName,
                DataAccessScope = RoleDataAccessScope.Department
            });

            TempData["SecurityMessage"] = result.Succeeded ? "نقش جدید ایجاد شد." : "ایجاد نقش ناموفق بود.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteRole(string id, CancellationToken cancellationToken)
        {
            var role = await _roleManager.FindByIdAsync(id);
            if (role == null)
            {
                return NotFound();
            }

            var userCount = await _context.UserRoles.CountAsync(item => item.RoleId == id, cancellationToken);
            if (userCount > 0)
            {
                TempData["SecurityMessage"] = "ابتدا کاربران نقش را تغییر دهید، سپس حذف انجام می‌شود.";
                return RedirectToAction(nameof(Index));
            }

            _context.RolePermissions.RemoveRange(_context.RolePermissions.Where(item => item.RoleId == id));
            await _context.SaveChangesAsync(cancellationToken);

            var result = await _roleManager.DeleteAsync(role);
            TempData["SecurityMessage"] = result.Succeeded ? "نقش حذف شد." : "حذف نقش ناموفق بود.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RenameRole(string id, string newName, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(newName))
            {
                TempData["SecurityMessage"] = "نام جدید نقش معتبر نیست.";
                return RedirectToAction(nameof(Index));
            }

            var role = await _roleManager.FindByIdAsync(id);
            if (role == null)
            {
                return NotFound();
            }

            var normalized = newName.Trim();
            if (!string.Equals(role.Name, normalized, StringComparison.OrdinalIgnoreCase) &&
                await _roleManager.RoleExistsAsync(normalized))
            {
                TempData["SecurityMessage"] = "نام نقش تکراری است.";
                return RedirectToAction(nameof(Index));
            }

            role.Name = normalized;
            role.NormalizedName = normalized.ToUpperInvariant();
            var result = await _roleManager.UpdateAsync(role);
            TempData["SecurityMessage"] = result.Succeeded ? "نام نقش بروزرسانی شد." : "ویرایش نقش ناموفق بود.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Matrix(CancellationToken cancellationToken)
        {
            var roles = await _roleManager.Roles
                .AsNoTracking()
                .OrderBy(item => item.Name)
                .Select(item => new SecurityRoleVM
                {
                    Id = item.Id,
                    Name = item.Name ?? "Role"
                })
                .ToListAsync(cancellationToken);

            var roleIds = roles.Select(item => item.Id).ToList();
            var permissions = await _context.RolePermissions
                .AsNoTracking()
                .Where(item => roleIds.Contains(item.RoleId))
                .ToListAsync(cancellationToken);

            var matrix = new SecurityMatrixVM
            {
                Roles = roles,
                Features = PermissionCatalog.CorePermissions
                    .Select(item => new PermissionFeatureVM
                    {
                        Key = item.Key,
                        Area = item.Category,
                        Title = item.DisplayName
                    })
                    .ToList()
            };

            ViewBag.PermissionMap = permissions
                .GroupBy(item => $"{item.RoleId}:{item.PermissionKey}")
                .ToDictionary(group => group.Key, group => group.OrderByDescending(item => item.Id).First().IsAllowed);

            return View(matrix);
        }

        [HttpPost("Admin/Security/TogglePermission")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TogglePermission(SecurityPermissionToggleVM model, CancellationToken cancellationToken)
        {
            var isAjaxRequest = string.Equals(
                Request.Headers["X-Requested-With"],
                "XMLHttpRequest",
                StringComparison.OrdinalIgnoreCase);

            if (!ModelState.IsValid)
            {
                if (isAjaxRequest)
                {
                    return BadRequest(new { success = false, message = "اطلاعات ارسالی معتبر نیست." });
                }

                return RedirectToAction(nameof(Matrix));
            }

            var permissionExists = await _context.Permissions.AnyAsync(item => item.Key == model.PermissionKey, cancellationToken);
            var roleExists = await _roleManager.Roles.AnyAsync(item => item.Id == model.RoleId, cancellationToken);
            if (!permissionExists || !roleExists)
            {
                if (isAjaxRequest)
                {
                    return BadRequest(new { success = false, message = "نقش یا مجوز انتخاب شده معتبر نیست." });
                }

                return RedirectToAction(nameof(Matrix));
            }

            var entity = await _context.RolePermissions
                .FirstOrDefaultAsync(item => item.RoleId == model.RoleId && item.PermissionKey == model.PermissionKey, cancellationToken);

            if (entity == null)
            {
                entity = new RolePermission
                {
                    RoleId = model.RoleId,
                    PermissionKey = model.PermissionKey,
                    IsAllowed = model.IsAllowed
                };
                _context.RolePermissions.Add(entity);
            }
            else
            {
                entity.IsAllowed = model.IsAllowed;
            }

            await _context.SaveChangesAsync(cancellationToken);

            if (isAjaxRequest)
            {
                return Json(new
                {
                    success = true,
                    isAllowed = model.IsAllowed,
                    message = model.IsAllowed ? "دسترسی فعال شد." : "دسترسی غیرفعال شد."
                });
            }

            return RedirectToAction(nameof(Matrix));
        }

        [HttpGet]
        public async Task<IActionResult> WorkflowRoutes(CancellationToken cancellationToken)
        {
            var routes = await _context.WorkflowRoutes
                .AsNoTracking()
                .Include(item => item.ApproverUser)
                .OrderBy(item => item.DocumentType)
                .ThenBy(item => item.StepNumber)
                .ToListAsync(cancellationToken);

            ViewBag.UserOptions = await _context.Users
                .AsNoTracking()
                .OrderBy(item => item.FullName)
                .Select(item => new { item.Id, item.FullName })
                .ToListAsync(cancellationToken);

            return View(routes);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateWorkflowRoute(string documentType, int stepNumber, string approverUserId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(documentType) || string.IsNullOrWhiteSpace(approverUserId) || stepNumber <= 0)
            {
                TempData["SecurityMessage"] = "اطلاعات مسیر گردش کار ناقص است.";
                return RedirectToAction(nameof(WorkflowRoutes));
            }

            var exists = await _context.WorkflowRoutes
                .AnyAsync(item => item.DocumentType == documentType.Trim() && item.StepNumber == stepNumber, cancellationToken);
            if (exists)
            {
                TempData["SecurityMessage"] = "برای این نوع سند، این گام قبلاً ثبت شده است.";
                return RedirectToAction(nameof(WorkflowRoutes));
            }

            _context.WorkflowRoutes.Add(new WorkflowRoute
            {
                DocumentType = documentType.Trim(),
                StepNumber = stepNumber,
                ApproverUserId = approverUserId,
                IsActive = true
            });
            await _context.SaveChangesAsync(cancellationToken);

            TempData["SecurityMessage"] = "گام گردش کار ثبت شد.";
            return RedirectToAction(nameof(WorkflowRoutes));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteWorkflowRoute(int id, CancellationToken cancellationToken)
        {
            var route = await _context.WorkflowRoutes.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (route == null)
            {
                return NotFound();
            }

            _context.WorkflowRoutes.Remove(route);
            await _context.SaveChangesAsync(cancellationToken);
            TempData["SecurityMessage"] = "گام گردش کار حذف شد.";
            return RedirectToAction(nameof(WorkflowRoutes));
        }
    }
}
