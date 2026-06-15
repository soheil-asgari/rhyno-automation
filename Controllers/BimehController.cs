using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Data;
using OfficeAutomation.Models;
using OfficeAutomation.Services.Security;

namespace OfficeAutomation.Controllers
{
    [Authorize]
    [PermissionAuthorize("HR.View")]
    public class BimehController : Controller
    {
        private readonly ApplicationDbContext _context;
        private static readonly string[] DefaultStatuses = ["Draft", "Submitted", "Approved", "Finalized"];

        public BimehController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        [PermissionAuthorize("HR.View")]
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

        [HttpGet]
        [PermissionAuthorize("HR.Create")]
        public IActionResult Create()
        {
            var now = DateTime.Now;
            var persianCalendar = new PersianCalendar();

            return View(new InsuranceCreateVM
            {
                Month = persianCalendar.GetMonth(now),
                Year = persianCalendar.GetYear(now)
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("HR.Create")]
        public async Task<IActionResult> Create(InsuranceCreateVM model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var request = new InsuranceSaveRequestViewModel
            {
                ProjectName = model.ProjectName,
                ManagerName = model.ManagerName,
                Month = model.Month,
                Year = model.Year,
                Status = "Draft",
                Employees = model.Employees ?? new List<InsuranceEmployeeRowViewModel>()
            };

            var saveResult = await SaveInsuranceInternalAsync(request, HttpContext.RequestAborted);
            if (!saveResult.Success)
            {
                ModelState.AddModelError(string.Empty, saveResult.Message ?? "خطا در ذخیره لیست بیمه.");
                return View(model);
            }

            TempData["InsuranceSuccess"] = "لیست بیمه با موفقیت ذخیره شد.";
            return RedirectToAction(nameof(Edit), new { id = saveResult.ListId });
        }

        [HttpGet]
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

        [HttpGet]
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
                    .Select(employee => new InsuranceEmployeeRowViewModel
                    {
                        HumanCapitalEmployeeId = employee.HumanCapitalEmployeeId,
                        FullName = employee.FullName,
                        JobTitle = employee.JobTitle,
                        StartWorkSolar = ToSolarDate(employee.StartWork),
                        EndWorkSolar = employee.EndWork.HasValue ? ToSolarDate(employee.EndWork.Value) : string.Empty,
                        WorkDays = employee.WorkDays,
                        Salary = employee.Salary
                    })
                    .ToList()
            };

            if (model.Employees.Count == 0)
            {
                model.Employees.Add(new InsuranceEmployeeRowViewModel { StartWorkSolar = ToSolarDate(DateTime.Now) });
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("HR.Edit")]
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

            var request = new InsuranceSaveRequestViewModel
            {
                Id = model.Id,
                ProjectName = model.ProjectName,
                ManagerName = model.ManagerName,
                Month = model.Month,
                Year = model.Year,
                Status = model.Status,
                Employees = model.Employees ?? new List<InsuranceEmployeeRowViewModel>()
            };

            var saveResult = await SaveInsuranceInternalAsync(request, HttpContext.RequestAborted);
            if (!saveResult.Success)
            {
                ModelState.AddModelError(string.Empty, saveResult.Message ?? "خطا در ذخیره لیست بیمه.");
                model.AvailableStatuses = await GetAvailableStatusesAsync();
                return View(model);
            }

            TempData["InsuranceSuccess"] = "لیست بیمه با موفقیت ذخیره شد.";
            return RedirectToAction(nameof(Edit), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveInsuranceAjax([FromBody] InsuranceSaveRequestViewModel request, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { success = false, message = "اطلاعات ارسالی معتبر نیست." });
            }

            var saveResult = await SaveInsuranceInternalAsync(request, cancellationToken);
            if (!saveResult.Success)
            {
                return BadRequest(new { success = false, message = saveResult.Message });
            }

            return Json(new
            {
                success = true,
                message = "لیست بیمه با موفقیت ذخیره شد.",
                id = saveResult.ListId
            });
        }

        [HttpGet]
        public async Task<IActionResult> CopyFromPreviousMonth(int month, int year, CancellationToken cancellationToken)
        {
            if (month is < 1 or > 12 || year is < 1300 or > 1600)
            {
                return BadRequest(new { success = false, message = "ماه یا سال معتبر نیست." });
            }

            var previousMonth = month == 1 ? 12 : month - 1;
            var previousYear = month == 1 ? year - 1 : year;

            var previousList = await _context.InsuranceLists
                .AsNoTracking()
                .Include(item => item.Employees)
                .Where(item =>
                    item.Month == previousMonth &&
                    item.Year == previousYear &&
                    (item.Status == "Approved" || item.Status == "Finalized"))
                .OrderByDescending(item => item.CreatedDate)
                .FirstOrDefaultAsync(cancellationToken);

            if (previousList == null)
            {
                return NotFound(new { success = false, message = "لیست نهایی‌شده‌ای برای ماه قبل یافت نشد." });
            }

            return Json(new
            {
                success = true,
                message = "اطلاعات ماه قبل با موفقیت دریافت شد.",
                data = new
                {
                    projectName = previousList.ProjectName,
                    managerName = previousList.ManagerName,
                    employees = previousList.Employees
                        .OrderBy(employee => employee.FullName)
                        .Select(employee => new
                        {
                            fullName = employee.FullName,
                            jobTitle = employee.JobTitle,
                            startWorkSolar = ToSolarDate(employee.StartWork),
                            endWorkSolar = employee.EndWork.HasValue ? ToSolarDate(employee.EndWork.Value) : string.Empty,
                            workDays = employee.WorkDays,
                            salary = employee.Salary
                        })
                }
            });
        }

        [HttpGet]
        public IActionResult ParseSolarDate(string value)
        {
            if (!TryParseSolarDate(value, out var gregorianDate))
            {
                return BadRequest(new { success = false, message = "فرمت تاریخ شمسی نامعتبر است." });
            }

            return Json(new
            {
                success = true,
                gregorian = gregorianDate.ToString("yyyy-MM-dd"),
                solar = ToSolarDate(gregorianDate)
            });
        }

        [HttpGet]
        public IActionResult SolarToday()
        {
            var today = DateTime.Now.Date;
            return Json(new
            {
                success = true,
                solar = ToSolarDate(today),
                gregorian = today.ToString("yyyy-MM-dd")
            });
        }

        [HttpGet]
        public IActionResult Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var list = _context.InsuranceLists
                .AsNoTracking()
                .Include(item => item.Employees)
                .FirstOrDefault(item => item.Id == id.Value);

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

        private async Task<(bool Success, int ListId, string? Message)> SaveInsuranceInternalAsync(
            InsuranceSaveRequestViewModel request,
            CancellationToken cancellationToken)
        {
            var rows = (request.Employees ?? new List<InsuranceEmployeeRowViewModel>())
                .Where(row => row.HumanCapitalEmployeeId.HasValue)
                .ToList();

            if (rows.Count == 0)
            {
                return (false, 0, "حداقل یک کارمند باید ثبت شود.");
            }

            var employees = new List<InsuranceEmployee>();
            var hrEmployeeIds = rows
                .Where(row => row.HumanCapitalEmployeeId.HasValue)
                .Select(row => row.HumanCapitalEmployeeId!.Value)
                .Distinct()
                .ToList();

            var hrEmployees = await _context.HumanCapitalEmployees
                .AsNoTracking()
                .Where(item => hrEmployeeIds.Contains(item.Id) && item.CurrentStatus == "فعال")
                .ToDictionaryAsync(item => item.Id, cancellationToken);

            if (hrEmployees.Count != hrEmployeeIds.Count)
            {
                return (false, 0, "برخی پرسنل انتخابی معتبر یا فعال نیستند.");
            }

            foreach (var row in rows)
            {
                var hrEmployee = hrEmployees[row.HumanCapitalEmployeeId!.Value];
                var startWork = hrEmployee.HireDate.Date;

                DateTime? endWork = null;
                if (!string.IsNullOrWhiteSpace(row.EndWorkSolar))
                {
                    if (!TryParseSolarDate(row.EndWorkSolar, out var parsedEndWork))
                    {
                        return (false, 0, $"تاریخ ترک کار برای {row.FullName} معتبر نیست.");
                    }

                    endWork = parsedEndWork;
                }

                employees.Add(new InsuranceEmployee
                {
                    HumanCapitalEmployeeId = row.HumanCapitalEmployeeId,
                    FullName = hrEmployee.FullName.Trim(),
                    JobTitle = hrEmployee.PositionTitle.Trim(),
                    StartWork = startWork,
                    EndWork = endWork,
                    WorkDays = row.WorkDays,
                    Salary = row.Salary
                });
            }

            InsuranceList? list = null;

            if (request.Id.HasValue && request.Id.Value > 0)
            {
                list = await _context.InsuranceLists
                    .FirstOrDefaultAsync(item => item.Id == request.Id.Value, cancellationToken);
            }

            if (list == null)
            {
                list = new InsuranceList
                {
                    CreatedDate = DateTime.Now
                };

                _context.InsuranceLists.Add(list);
            }

            list.ProjectName = request.ProjectName.Trim();
            list.ManagerName = request.ManagerName.Trim();
            list.Month = request.Month;
            list.Year = request.Year;
            list.Status = string.IsNullOrWhiteSpace(request.Status) ? "Draft" : request.Status.Trim();
            list.EmployeeCount = employees.Count;

            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            await _context.SaveChangesAsync(cancellationToken);

            await _context.InsuranceEmployees
                .Where(employee => employee.InsuranceListId == list.Id)
                .ExecuteDeleteAsync(cancellationToken);

            foreach (var employee in employees)
            {
                employee.InsuranceListId = list.Id;
            }

            _context.InsuranceEmployees.AddRange(employees);
            await _context.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return (true, list.Id, null);
        }

        private static bool TryParseSolarDate(string? solarDate, out DateTime gregorianDate)
        {
            gregorianDate = default;
            if (string.IsNullOrWhiteSpace(solarDate))
            {
                return false;
            }

            var normalized = solarDate
                .Replace("۰", "0")
                .Replace("۱", "1")
                .Replace("۲", "2")
                .Replace("۳", "3")
                .Replace("۴", "4")
                .Replace("۵", "5")
                .Replace("۶", "6")
                .Replace("۷", "7")
                .Replace("۸", "8")
                .Replace("۹", "9")
                .Replace('-', '/')
                .Trim();

            var parts = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length != 3)
            {
                return false;
            }

            if (!int.TryParse(parts[0], out var year) ||
                !int.TryParse(parts[1], out var month) ||
                !int.TryParse(parts[2], out var day))
            {
                return false;
            }

            if (year is < 1300 or > 1600 || month is < 1 or > 12 || day is < 1 or > 31)
            {
                return false;
            }

            try
            {
                var persianCalendar = new PersianCalendar();
                gregorianDate = persianCalendar.ToDateTime(year, month, day, 0, 0, 0, 0).Date;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string ToSolarDate(DateTime dateTime)
        {
            var persianCalendar = new PersianCalendar();
            var year = persianCalendar.GetYear(dateTime);
            var month = persianCalendar.GetMonth(dateTime);
            var day = persianCalendar.GetDayOfMonth(dateTime);
            return $"{year:0000}/{month:00}/{day:00}";
        }

        [HttpGet]
        public async Task<IActionResult> GetEmployees(string? search)
        {
            var query = _context.HumanCapitalEmployees
                .AsNoTracking()
                .Where(e => e.CurrentStatus == "فعال");

            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchTerm = search.Trim().ToLower();
                query = query.Where(e => 
                    e.FullName.ToLower().Contains(searchTerm) ||
                    e.PersonnelCode.ToLower().Contains(searchTerm));
            }

            var employees = await query
                .OrderBy(e => e.FullName)
                .Take(50)
                .Select(e => new
                {
                    id = e.Id,
                    personnelCode = e.PersonnelCode,
                    fullName = e.FullName,
                    positionTitle = e.PositionTitle,
                    currentSalary = e.CurrentSalary,
                    hireDateShamsi = ToSolarDate(e.HireDate)
                })
                .ToListAsync();

            return Json(employees);
        }

        [HttpGet]
        public async Task<IActionResult> GetEmployeeById(int id)
        {
            var employee = await _context.HumanCapitalEmployees
                .AsNoTracking()
                .Where(e => e.Id == id)
                .Select(e => new
                {
                    id = e.Id,
                    personnelCode = e.PersonnelCode,
                    fullName = e.FullName,
                    positionTitle = e.PositionTitle,
                    currentSalary = e.CurrentSalary,
                    hireDate = e.HireDate,
                    hireDateShamsi = ToSolarDate(e.HireDate)
                })
                .FirstOrDefaultAsync();

            if (employee == null)
            {
                return NotFound();
            }

            return Json(employee);
        }
    }
}
