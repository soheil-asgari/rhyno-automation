using OfficeAutomation.Services.Licensing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Modules.Identity.Infrastructure.Persistence;
using OfficeAutomation.Modules.Workflow.Infrastructure.Persistence;
using OfficeAutomation.Filters;
using OfficeAutomation.Models;
using OfficeAutomation.Services.Security;

namespace OfficeAutomation.Controllers
{
    [Authorize]
    [PermissionAuthorize("Security.Manage")]
    [LicenseExempt]
public class SecurityController : Controller
    {
        private readonly IdentityDbContext _context;
        private readonly WorkflowDbContext _workflowContext;
        private readonly RoleManager<ApplicationRole> _roleManager;

        public SecurityController(IdentityDbContext context, WorkflowDbContext workflowContext, RoleManager<ApplicationRole> roleManager)
        {
            _context = context;
            _workflowContext = workflowContext;
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
            var routes = await _workflowContext.WorkflowRoutes
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
        public async Task<IActionResult> CreateWorkflowRoute(string documentType, int stepNumber, string? approverUserId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(documentType) || string.IsNullOrWhiteSpace(approverUserId) || stepNumber <= 0)
            {
                TempData["SecurityMessage"] = "اطلاعات مسیر گردش کار ناقص است.";
                return RedirectToAction(nameof(WorkflowRoutes));
            }

            var exists = await _workflowContext.WorkflowRoutes
                .AnyAsync(item => item.DocumentType == documentType.Trim() && item.StepNumber == stepNumber, cancellationToken);
            if (exists)
            {
                TempData["SecurityMessage"] = "برای این نوع سند، این گام قبلاً ثبت شده است.";
                return RedirectToAction(nameof(WorkflowRoutes));
            }

            _workflowContext.WorkflowRoutes.Add(new WorkflowRoute
            {
                DocumentType = documentType.Trim(),
                StepNumber = stepNumber,
                StepName = $"مرحله {stepNumber}",
                AssignmentMode = WorkflowAssignmentMode.User,
                ApproverUserId = approverUserId,
                IsActive = true
            });
            await _workflowContext.SaveChangesAsync(cancellationToken);

            TempData["SecurityMessage"] = "گام گردش کار ثبت شد.";
            return RedirectToAction(nameof(WorkflowRoutes));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteWorkflowRoute(int id, CancellationToken cancellationToken)
        {
            var route = await _workflowContext.WorkflowRoutes.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (route == null)
            {
                return NotFound();
            }

            _workflowContext.WorkflowRoutes.Remove(route);
            await _workflowContext.SaveChangesAsync(cancellationToken);
            TempData["SecurityMessage"] = "گام گردش کار حذف شد.";
            return RedirectToAction(nameof(WorkflowRoutes));
        }

        [HttpGet]
        public async Task<IActionResult> WorkflowBuilder(string? documentType, CancellationToken cancellationToken)
        {
            var docType = documentType ?? "Letter";
            ViewBag.DocumentType = docType;
            ViewBag.UserOptions = await _context.Users
                .AsNoTracking()
                .OrderBy(item => item.FullName)
                .Select(item => new { item.Id, item.FullName })
                .ToListAsync(cancellationToken);

            ViewBag.RoleOptions = await _roleManager.Roles
                .AsNoTracking()
                .OrderBy(item => item.Name)
                .Select(item => new { item.Id, Name = item.Name ?? "Role" })
                .ToListAsync(cancellationToken);

            ViewBag.DepartmentOptions = await _context.Departments
                .AsNoTracking()
                .OrderBy(item => item.Name)
                .Select(item => new { item.Id, item.Name })
                .ToListAsync(cancellationToken);

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetWorkflowGraph(string documentType, CancellationToken cancellationToken)
        {
            var routes = await _workflowContext.WorkflowRoutes
                .AsNoTracking()
                .Where(item => item.DocumentType == documentType)
                .OrderBy(item => item.StepNumber)
                .ToListAsync(cancellationToken);

            var nodes = new List<ReactFlowNodeDto>();
            var edges = new List<ReactFlowEdgeDto>();

            // Always add a Start Node
            nodes.Add(new ReactFlowNodeDto
            {
                Id = "start",
                Type = "StartEndNode",
                Label = "شروع گردش",
                X = 100,
                Y = 200
            });

            var previousNodeId = "start";

            for (int i = 0; i < routes.Count; i++)
            {
                var route = routes[i];
                var nodeId = $"step-{route.Id}";

                double x = 300 + (i * 200);
                double y = 200;

                // Attempt to deserialize coordinates if present in UiMetadata
                if (!string.IsNullOrWhiteSpace(route.UiMetadata))
                {
                    try
                    {
                        var coords = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.Nodes.JsonObject>(route.UiMetadata);
                        if (coords != null)
                        {
                            if (coords.TryGetPropertyValue("x", out var xVal) && xVal != null) x = xVal.GetValue<double>();
                            if (coords.TryGetPropertyValue("y", out var yVal) && yVal != null) y = yVal.GetValue<double>();
                        }
                    }
                    catch { }
                }

                nodes.Add(new ReactFlowNodeDto
                {
                    Id = nodeId,
                    Type = "ApprovalNode",
                    Label = route.StepName ?? $"مرحله {route.StepNumber}",
                    X = x,
                    Y = y,
                    AssignmentMode = route.AssignmentMode,
                    AssignedToUserId = route.ApproverUserId,
                    AssignedRoleId = route.ApproverRoleId,
                    AssignedDepartmentId = route.ApproverDepartmentId,
                    SlaHours = route.SlaHours,
                    EscalationHours = route.EscalationHours
                });

                edges.Add(new ReactFlowEdgeDto
                {
                    Id = $"edge-{previousNodeId}-{nodeId}",
                    Source = previousNodeId,
                    Target = nodeId
                });

                previousNodeId = nodeId;
            }

            // Add an End Node
            var endNodeId = "end";
            nodes.Add(new ReactFlowNodeDto
            {
                Id = endNodeId,
                Type = "StartEndNode",
                Label = "پایان گردش",
                X = 300 + (routes.Count * 200) + 200,
                Y = 200
            });

            edges.Add(new ReactFlowEdgeDto
            {
                Id = $"edge-{previousNodeId}-{endNodeId}",
                Source = previousNodeId,
                Target = endNodeId
            });

            return Json(new { documentType, nodes, edges });
        }

        [HttpPost]
        public async Task<IActionResult> SaveWorkflowGraph([FromBody] ReactFlowGraphDto graph, CancellationToken cancellationToken)
        {
            if (graph == null || string.IsNullOrWhiteSpace(graph.DocumentType))
            {
                return BadRequest(new { success = false, message = "مدل ورودی معتبر نیست." });
            }

            // Retrieve existing routes for this documentType
            var existingRoutes = await _workflowContext.WorkflowRoutes
                .Where(item => item.DocumentType == graph.DocumentType)
                .ToListAsync(cancellationToken);

            // Filter nodes to exclude start/end nodes
            var approvalNodes = graph.Nodes
                .Where(n => n.Type != "StartEndNode")
                .ToList();

            // Re-map routing steps based on sequence in graph or topological order of edges.
            // For simplicity and robustness, we map them in order of x coordinate (left to right)
            var sortedNodes = approvalNodes.OrderBy(n => n.X).ToList();

            // Delete nodes that are no longer present
            var activeIds = sortedNodes
                .Where(n => n.Id.StartsWith("step-"))
                .Select(n => int.Parse(n.Id.Replace("step-", "")))
                .ToList();

            var toRemove = existingRoutes.Where(r => !activeIds.Contains(r.Id)).ToList();
            _workflowContext.WorkflowRoutes.RemoveRange(toRemove);

            for (int i = 0; i < sortedNodes.Count; i++)
            {
                var node = sortedNodes[i];
                var stepNum = i + 1;

                var coordsJson = System.Text.Json.JsonSerializer.Serialize(new { x = node.X, y = node.Y });

                if (node.Id.StartsWith("step-"))
                {
                    // Update existing
                    var routeId = int.Parse(node.Id.Replace("step-", ""));
                    var route = existingRoutes.FirstOrDefault(r => r.Id == routeId);
                    if (route != null)
                    {
                        route.StepNumber = stepNum;
                        route.StepName = node.Label;
                        route.AssignmentMode = node.AssignmentMode ?? WorkflowAssignmentMode.User;
                        route.ApproverUserId = node.AssignedToUserId;
                        route.ApproverRoleId = node.AssignedRoleId;
                        route.ApproverDepartmentId = node.AssignedDepartmentId;
                        route.SlaHours = node.SlaHours;
                        route.EscalationHours = node.EscalationHours;
                        route.UiMetadata = coordsJson;
                    }
                }
                else
                {
                    // Create new
                    var route = new WorkflowRoute
                    {
                        DocumentType = graph.DocumentType,
                        StepNumber = stepNum,
                        StepName = node.Label,
                        AssignmentMode = node.AssignmentMode ?? WorkflowAssignmentMode.User,
                        ApproverUserId = node.AssignedToUserId,
                        ApproverRoleId = node.AssignedRoleId,
                        ApproverDepartmentId = node.AssignedDepartmentId,
                        SlaHours = node.SlaHours,
                        EscalationHours = node.EscalationHours,
                        IsActive = true,
                        UiMetadata = coordsJson
                    };
                    _workflowContext.WorkflowRoutes.Add(route);
                }
            }

            await _workflowContext.SaveChangesAsync(cancellationToken);
            return Json(new { success = true, message = "گردش کار با موفقیت ذخیره شد." });
        }
    }
}

