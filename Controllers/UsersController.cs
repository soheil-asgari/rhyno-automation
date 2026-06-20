using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Data;
using OfficeAutomation.Models;
using OfficeAutomation.Services.Security;

namespace OfficeAutomation.Controllers
{
    [Authorize]
    [PermissionAuthorize("Users.Manage")]
    public class UsersController : Controller
    {
        private readonly UserManager<User> _userManager;
        private readonly RoleManager<ApplicationRole> _roleManager;
        private readonly ApplicationDbContext _context;

        public UsersController(UserManager<User> userManager, RoleManager<ApplicationRole> roleManager, ApplicationDbContext context)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var users = await _userManager.Users
                .Include(item => item.Employee)
                .Include(item => item.ParentManagerUser)
                .ToListAsync();
            return View(users);
        }

        public async Task<IActionResult> Create(CancellationToken cancellationToken)
        {
            await PopulateEmployeesAsync(cancellationToken);
            await PopulateManagersAsync(cancellationToken);
            await PopulateRolesAsync(cancellationToken);
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(UserViewModel model, CancellationToken cancellationToken)
        {
            if (!model.EmployeeId.HasValue)
            {
                ModelState.AddModelError(nameof(model.EmployeeId), "انتخاب کارمند الزامی است.");
            }

            HumanCapitalEmployee? employee = null;
            if (model.EmployeeId.HasValue)
            {
                employee = await _context.HumanCapitalEmployees
                    .AsNoTracking()
                    .FirstOrDefaultAsync(item => item.Id == model.EmployeeId.Value && item.CurrentStatus == "فعال", cancellationToken);

                if (employee == null)
                {
                    ModelState.AddModelError(nameof(model.EmployeeId), "کارمند انتخابی معتبر یا فعال نیست.");
                }
                else
                {
                    var alreadyLinked = await _context.Users
                        .AsNoTracking()
                        .AnyAsync(item => item.EmployeeId == model.EmployeeId.Value, cancellationToken);
                    if (alreadyLinked)
                    {
                        ModelState.AddModelError(nameof(model.EmployeeId), "برای این کارمند قبلاً حساب کاربری تعریف شده است.");
                    }
                }
            }

            if (ModelState.IsValid)
            {
                var user = new User
                {
                    UserName = model.Email,
                    Email = model.Email,
                    FullName = model.FullName,
                    EmployeeId = model.EmployeeId,
                    DepartmentId = employee?.DepartmentId,
                    JobTitle = employee?.PositionTitle,
                    ParentManagerUserId = model.ParentManagerUserId,
                    EmailConfirmed = true
                };

                var result = await _userManager.CreateAsync(user, model.Password ?? string.Empty);

                if (result.Succeeded)
                {
                    if (!string.IsNullOrWhiteSpace(model.Role) && await _roleManager.RoleExistsAsync(model.Role))
                    {
                        await _userManager.AddToRoleAsync(user, model.Role);
                    }

                    TempData["SuccessMessage"] = "کاربر جدید با موفقیت تعریف شد.";
                    return RedirectToAction(nameof(Index));
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            await PopulateEmployeesAsync(cancellationToken);
            await PopulateManagersAsync(cancellationToken);
            await PopulateRolesAsync(cancellationToken);
            return View(model);
        }

        private async Task PopulateEmployeesAsync(CancellationToken cancellationToken)
        {
            var employees = await _context.HumanCapitalEmployees
                .AsNoTracking()
                .Where(item => item.CurrentStatus == "فعال")
                .OrderBy(item => item.FullName)
                .Select(item => new
                {
                    item.Id,
                    item.FullName,
                    item.PersonnelCode
                })
                .ToListAsync(cancellationToken);

            var usedEmployeeIds = await _context.Users
                .AsNoTracking()
                .Where(item => item.EmployeeId.HasValue)
                .Select(item => item.EmployeeId!.Value)
                .ToListAsync(cancellationToken);

            ViewBag.EmployeeOptions = employees
                .Where(item => !usedEmployeeIds.Contains(item.Id))
                .ToList();
        }

        private async Task PopulateManagersAsync(CancellationToken cancellationToken)
        {
            ViewBag.ManagerOptions = await _context.Users
                .AsNoTracking()
                .OrderBy(item => item.FullName)
                .Select(item => new
                {
                    item.Id,
                    item.FullName
                })
                .ToListAsync(cancellationToken);
        }

        private async Task PopulateRolesAsync(CancellationToken cancellationToken)
        {
            ViewBag.RoleOptions = await _roleManager.Roles
                .AsNoTracking()
                .OrderBy(item => item.Name)
                .Select(item => item.Name)
                .Where(item => item != null)
                .ToListAsync(cancellationToken);

            ViewBag.RolePermissionMap = new Dictionary<string, string[]>
            {
                ["Admin"] = ["Security.Manage", "Users.Manage", "Letters.Read", "Finance.View", "Warehouse.View", "HR.View", "SystemSettings.View"],
                ["FinanceManager"] = ["Finance.View", "Finance.Create", "Finance.Edit", "Finance.Delete", "Finance.Export"],
                ["WarehouseManager"] = ["Warehouse.View", "Warehouse.Create", "Warehouse.Edit", "Warehouse.Delete", "Warehouse.Export"],
                ["HrManager"] = ["HR.View", "HR.Create", "HR.Edit", "HR.Delete", "HR.Export"]
            };
        }
    }
}
