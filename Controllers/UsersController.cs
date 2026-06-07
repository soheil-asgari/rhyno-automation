using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OfficeAutomation.Models;
using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Data;

namespace OfficeAutomation.Controllers
{
    [Authorize] // فقط افراد وارد شده دسترسی داشته باشند
    public class UsersController : Controller
    {
        private readonly UserManager<User> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ApplicationDbContext _context;

        public UsersController(UserManager<User> userManager, RoleManager<IdentityRole> roleManager, ApplicationDbContext context)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
        }

        // ۱. نمایش لیست تمام کاربران
        public async Task<IActionResult> Index()
        {
            var users = await _userManager.Users
                .Include(item => item.Employee)
                .Include(item => item.ParentManagerUser)
                .ToListAsync();
            return View(users);
        }

        // ۲. صفحه فرم ایجاد کاربر جدید (GET)
        public async Task<IActionResult> Create(CancellationToken cancellationToken)
        {
            await PopulateEmployeesAsync(cancellationToken);
            await PopulateManagersAsync(cancellationToken);
            await PopulateRolesAsync(cancellationToken);
            return View();
        }

        // ۳. عملیات ذخیره کاربر جدید (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(UserViewModel model, CancellationToken cancellationToken)
        {
            if (!model.EmployeeId.HasValue)
            {
                ModelState.AddModelError(nameof(model.EmployeeId), "انتخاب کارمند الزامی است.");
            }

            var employee = default(HumanCapitalEmployee);
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
                var permissionTemplate = await ResolveRolePermissionTemplateAsync(model.Role, cancellationToken);
                var user = new User
                {
                    UserName = model.Email,
                    Email = model.Email,
                    FullName = model.FullName,
                    EmployeeId = model.EmployeeId,
                    JobTitle = employee?.PositionTitle,
                    CanAccessFinance = permissionTemplate.Finance,
                    CanAccessWarehouse = permissionTemplate.Warehouse,
                    CanAccessHumanCapital = permissionTemplate.HumanCapital,
                    CanAccessSystemSettings = permissionTemplate.SystemSettings,
                    ParentManagerUserId = model.ParentManagerUserId,
                    EmailConfirmed = true // چون خودمان می‌سازیم تایید شده فرض می‌کنیم
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
                    ModelState.AddModelError("", error.Description);
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

            employees = employees
                .Where(item => !usedEmployeeIds.Contains(item.Id))
                .ToList();

            ViewBag.EmployeeOptions = employees;
        }

        private async Task PopulateManagersAsync(CancellationToken cancellationToken)
        {
            var managers = await _context.Users
                .AsNoTracking()
                .OrderBy(item => item.FullName)
                .Select(item => new
                {
                    item.Id,
                    item.FullName
                })
                .ToListAsync(cancellationToken);

            ViewBag.ManagerOptions = managers;
        }

        private async Task PopulateRolesAsync(CancellationToken cancellationToken)
        {
            var roles = await _roleManager.Roles
                .AsNoTracking()
                .OrderBy(item => item.Name)
                .Select(item => item.Name)
                .Where(item => item != null)
                .ToListAsync(cancellationToken);

            ViewBag.RoleOptions = roles!;
        }

        private async Task<(bool Finance, bool Warehouse, bool HumanCapital, bool SystemSettings)> ResolveRolePermissionTemplateAsync(string? roleName, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(roleName))
            {
                return (false, false, false, false);
            }

            var role = await _roleManager.FindByNameAsync(roleName);
            if (role == null)
            {
                return (false, false, false, false);
            }

            var rolePermissions = await _context.RolePermissions
                .AsNoTracking()
                .Where(item => item.RoleId == role.Id)
                .ToListAsync(cancellationToken);

            bool Resolve(string key) => rolePermissions
                .Where(item => item.PermissionKey == key)
                .OrderByDescending(item => item.Id)
                .Select(item => item.IsAllowed)
                .FirstOrDefault();

            return (
                Resolve("Finance"),
                Resolve("Warehouse"),
                Resolve("HumanCapital"),
                Resolve("SystemSettings") || Resolve("WorkflowAdministration"));
        }
    }
}
