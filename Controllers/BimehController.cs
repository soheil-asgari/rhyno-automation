using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Data;
using OfficeAutomation.Models;

namespace OfficeAutomation.Controllers
{
    public class BimehController : Controller
    {
        private readonly ApplicationDbContext _context;
        private static readonly string[] DefaultStatuses = ["Draft", "Submitted", "Approved"];

        public BimehController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(InsuranceIndexVM filter)
        {
            var query = _context.InsuranceLists
                .AsNoTracking()
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
            {
                var searchTerm = filter.SearchTerm.Trim();
                query = query.Where(item =>
                    item.ProjectName.Contains(searchTerm) ||
                    item.ManagerName.Contains(searchTerm));
            }

            if (filter.Month.HasValue)
            {
                query = query.Where(item => item.Month == filter.Month.Value);
            }

            if (filter.Year.HasValue)
            {
                query = query.Where(item => item.Year == filter.Year.Value);
            }

            if (!string.IsNullOrWhiteSpace(filter.Status))
            {
                var status = filter.Status.Trim();
                query = query.Where(item => item.Status == status);
            }

            filter.TotalCount = await _context.InsuranceLists.CountAsync();
            filter.FilteredCount = await query.CountAsync();
            filter.Items = await query
                .OrderByDescending(item => item.CreatedDate)
                .ToListAsync();
            filter.AvailableStatuses = await GetAvailableStatusesAsync();

            return View(filter);
        }

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(InsuranceCreateVM model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var employees = (model.Employees ?? new List<InsuranceEmployee>())
                .Where(employee =>
                    !string.IsNullOrWhiteSpace(employee.FullName) &&
                    !string.IsNullOrWhiteSpace(employee.JobTitle))
                .ToList();

            if (employees.Count == 0)
            {
                ModelState.AddModelError(nameof(model.Employees), "حداقل یک کارمند باید ثبت شود.");
                return View(model);
            }

            var list = new InsuranceList
            {
                ProjectName = model.ProjectName,
                ManagerName = model.ManagerName,
                Month = model.Month,
                Year = model.Year,
                EmployeeCount = employees.Count,
                Status = "Draft",
                CreatedDate = DateTime.Now,
                Employees = employees
            };

            _context.InsuranceLists.Add(list);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var list = await _context.InsuranceLists
                .AsNoTracking()
                .Include(item => item.Employees)
                .FirstOrDefaultAsync(item => item.Id == id.Value);

            if (list == null)
            {
                return NotFound();
            }

            list.Employees = list.Employees
                .OrderBy(employee => employee.FullName)
                .ToList();

            return View(list);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var list = await _context.InsuranceLists
                .AsNoTracking()
                .Include(item => item.Employees)
                .FirstOrDefaultAsync(item => item.Id == id.Value);

            if (list == null)
            {
                return NotFound();
            }

            var model = new InsuranceEditVM
            {
                Id = list.Id,
                ProjectName = list.ProjectName,
                ManagerName = list.ManagerName,
                Month = list.Month,
                Year = list.Year,
                Status = list.Status,
                AvailableStatuses = await GetAvailableStatusesAsync(),
                Employees = list.Employees
                    .OrderBy(employee => employee.Id)
                    .Select(employee => new InsuranceEmployee
                    {
                        FullName = employee.FullName,
                        JobTitle = employee.JobTitle,
                        StartWork = employee.StartWork,
                        EndWork = employee.EndWork,
                        WorkDays = employee.WorkDays,
                        Salary = employee.Salary
                    })
                    .ToList()
            };

            if (model.Employees.Count == 0)
            {
                model.Employees.Add(new InsuranceEmployee());
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, InsuranceEditVM model)
        {
            if (id != model.Id)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                model.AvailableStatuses = await GetAvailableStatusesAsync();
                return View(model);
            }

            var employees = (model.Employees ?? new List<InsuranceEmployee>())
                .Where(employee =>
                    !string.IsNullOrWhiteSpace(employee.FullName) &&
                    !string.IsNullOrWhiteSpace(employee.JobTitle))
                .ToList();

            if (employees.Count == 0)
            {
                ModelState.AddModelError(nameof(model.Employees), "حداقل یک کارمند باید ثبت شود.");
                model.AvailableStatuses = await GetAvailableStatusesAsync();
                return View(model);
            }

            var existingList = await _context.InsuranceLists
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == id);

            if (existingList == null)
            {
                return NotFound();
            }

            var updatedList = new InsuranceList
            {
                Id = id,
                ProjectName = model.ProjectName,
                ManagerName = model.ManagerName,
                Month = model.Month,
                Year = model.Year,
                EmployeeCount = employees.Count,
                Status = model.Status,
                FilePath = existingList.FilePath,
                CreatedDate = existingList.CreatedDate
            };

            await using var transaction = await _context.Database.BeginTransactionAsync();

            _context.InsuranceLists.Update(updatedList);
            await _context.SaveChangesAsync();

            await _context.InsuranceEmployees
                .Where(employee => employee.InsuranceListId == id)
                .ExecuteDeleteAsync();

            foreach (var employee in employees)
            {
                employee.Id = 0;
                employee.InsuranceListId = id;
            }

            _context.InsuranceEmployees.AddRange(employees);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var list = await _context.InsuranceLists
                .AsNoTracking()
                .Include(item => item.Employees)
                .FirstOrDefaultAsync(item => item.Id == id.Value);

            if (list == null)
            {
                return NotFound();
            }

            return View(list);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var list = await _context.InsuranceLists.FindAsync(id);

            if (list != null)
            {
                _context.InsuranceLists.Remove(list);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        private async Task<List<string>> GetAvailableStatusesAsync()
        {
            var statuses = await _context.InsuranceLists
                .AsNoTracking()
                .Where(item => !string.IsNullOrWhiteSpace(item.Status))
                .Select(item => item.Status)
                .Distinct()
                .ToListAsync();

            var allStatuses = DefaultStatuses
                .Concat(statuses)
                .Where(status => !string.IsNullOrWhiteSpace(status))
                .Select(status => status.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(status => status)
                .ToList();

            return allStatuses;
        }
    }


}
