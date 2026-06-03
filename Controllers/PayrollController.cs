using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Data;
using OfficeAutomation.Models;

namespace OfficeAutomation.Controllers
{
    [Authorize]
    public class PayrollController : Controller
    {
        private readonly ApplicationDbContext _context;

        public PayrollController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index(int? month, int? year, CancellationToken cancellationToken)
        {
            var historyItems = await _context.PayrollLists
                .AsNoTracking()
                .Include(item => item.Items)
                .OrderByDescending(item => item.Year)
                .ThenByDescending(item => item.Month)
                .Select(item => new PayrollHistoryRowViewModel
                {
                    Id = item.Id,
                    Year = item.Year,
                    Month = item.Month,
                    TotalBaseSalary = item.Items.Sum(row => row.BaseSalary),
                    TotalInsurance = item.Items.Sum(row => row.InsuranceDeduction),
                    TotalNetPayable = item.Items.Sum(row => row.NetPayable),
                    Status = item.Status,
                    IsFinalized = item.IsFinalized
                })
                .ToListAsync(cancellationToken);

            return View(new PayrollHistoryIndexViewModel
            {
                Items = historyItems
            });
        }

        [HttpGet]
        public async Task<IActionResult> Create(int? month, int? year, CancellationToken cancellationToken)
        {
            var now = DateTime.Now;
            var activeMonth = month is >= 1 and <= 12 ? month.Value : now.Month;
            var activeYear = year is >= 1300 and <= 1600 ? year.Value : GetCurrentSolarYear(now);

            var payroll = await _context.PayrollLists
                .AsNoTracking()
                .Include(list => list.Items)
                .ThenInclude(item => item.HumanCapitalEmployee)
                .FirstOrDefaultAsync(list => list.Month == activeMonth && list.Year == activeYear, cancellationToken);

            var model = new PayrollListPageViewModel
            {
                Id = payroll?.Id,
                Month = activeMonth,
                Year = activeYear,
                IsFinalized = payroll?.IsFinalized ?? false,
                Status = payroll?.Status ?? "Draft",
                Items = payroll?.Items
                    .OrderBy(item => item.EmployeeName)
                    .Select(item => new PayrollEmployeeRowViewModel
                    {
                        Id = item.Id,
                        PayrollListId = item.PayrollListId,
                        HumanCapitalEmployeeId = item.HumanCapitalEmployeeId,
                        EmployeeName = item.EmployeeName,
                        JobTitle = item.HumanCapitalEmployee != null ? item.HumanCapitalEmployee.PositionTitle : null,
                        HireDateShamsi = item.HumanCapitalEmployee != null ? ToSolarDate(item.HumanCapitalEmployee.HireDate) : null,
                        BaseSalary = item.BaseSalary,
                        Allowance = item.Allowance,
                        Overtime = item.Overtime,
                        InsuranceDeduction = item.InsuranceDeduction,
                        Tax = item.Tax,
                        NetPayable = item.NetPayable,
                        IsLockedFromHr = item.HumanCapitalEmployeeId.HasValue
                    })
                    .ToList()
                    ?? new List<PayrollEmployeeRowViewModel>()
            };

            if (model.Items.Count == 0)
            {
                model.Items.Add(new PayrollEmployeeRowViewModel());
            }

            return View("Create", model);
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
        {
            var payroll = await _context.PayrollLists
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

            if (payroll == null)
            {
                return NotFound();
            }

            return RedirectToAction(nameof(Create), new
            {
                month = payroll.Month,
                year = payroll.Year
            });
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
        {
            var payroll = await _context.PayrollLists
                .AsNoTracking()
                .Include(item => item.Items)
                .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

            if (payroll == null)
            {
                return NotFound();
            }

            var details = new PayrollDetailsViewModel
            {
                Id = payroll.Id,
                Year = payroll.Year,
                Month = payroll.Month,
                Status = payroll.Status,
                IsFinalized = payroll.IsFinalized,
                TotalBaseSalary = payroll.Items.Sum(item => item.BaseSalary),
                TotalInsurance = payroll.Items.Sum(item => item.InsuranceDeduction),
                TotalNetPayable = payroll.Items.Sum(item => item.NetPayable),
                Items = payroll.Items
                    .OrderBy(item => item.EmployeeName)
                    .Select(item => new PayrollEmployeeRowViewModel
                    {
                        Id = item.Id,
                        HumanCapitalEmployeeId = item.HumanCapitalEmployeeId,
                        EmployeeName = item.EmployeeName,
                        BaseSalary = item.BaseSalary,
                        Allowance = item.Allowance,
                        Overtime = item.Overtime,
                        InsuranceDeduction = item.InsuranceDeduction,
                        Tax = item.Tax,
                        NetPayable = item.NetPayable
                    })
                    .ToList()
            };

            return View(details);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Calculate([FromBody] PayrollCalculationRequestViewModel request, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { success = false, message = "پارامترهای محاسبه معتبر نیست." });
            }

            var sourceEmployees = await _context.HumanCapitalEmployees
                .AsNoTracking()
                .OrderBy(employee => employee.FullName)
                .ToListAsync(cancellationToken);

            var rows = sourceEmployees
                .Select(employee =>
                {
                    var baseSalary = employee.CurrentSalary;
                    var allowance = 0m;
                    var overtime = 0m;
                    var insuranceDeduction = (baseSalary + allowance + overtime) * 0.07m;
                    var tax = 0m;
                    var netPayable = baseSalary + allowance + overtime - insuranceDeduction - tax;

                    return new PayrollEmployeeRowViewModel
                    {
                        HumanCapitalEmployeeId = employee.Id,
                        EmployeeName = employee.FullName,
                        JobTitle = employee.PositionTitle,
                        HireDateShamsi = ToSolarDate(employee.HireDate),
                        BaseSalary = Math.Round(baseSalary, 2),
                        Allowance = Math.Round(allowance, 2),
                        Overtime = Math.Round(overtime, 2),
                        InsuranceDeduction = Math.Round(insuranceDeduction, 2),
                        Tax = Math.Round(tax, 2),
                        NetPayable = Math.Round(netPayable, 2),
                        IsLockedFromHr = true
                    };
                })
                .ToList();

            if (rows.Count == 0)
            {
                rows.Add(new PayrollEmployeeRowViewModel());
            }

            return Json(new
            {
                success = true,
                message = "حقوق ماه انتخاب شده محاسبه شد.",
                items = rows
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save([FromBody] PayrollSaveRequestViewModel request, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { success = false, message = "اطلاعات ارسالی معتبر نیست." });
            }

            var rows = (request.Items ?? new List<PayrollEmployeeRowViewModel>())
                .Where(item => item.HumanCapitalEmployeeId.HasValue)
                .ToList();

            if (rows.Count == 0)
            {
                return BadRequest(new { success = false, message = "حداقل یک سطر حقوق باید ثبت شود." });
            }

            var payroll = await _context.PayrollLists
                .Include(list => list.Items)
                .FirstOrDefaultAsync(list => list.Month == request.Month && list.Year == request.Year, cancellationToken);

            if (payroll == null)
            {
                payroll = new PayrollList
                {
                    Month = request.Month,
                    Year = request.Year,
                    CreatedAt = DateTime.Now,
                    Status = "Draft"
                };

                _context.PayrollLists.Add(payroll);
            }

            payroll.IsFinalized = request.Finalize;
            payroll.Status = request.Finalize ? "Finalized" : "Draft";
            payroll.UpdatedAt = DateTime.Now;

            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            await _context.SaveChangesAsync(cancellationToken);

            await _context.PayrollItems
                .Where(item => item.PayrollListId == payroll.Id)
                .ExecuteDeleteAsync(cancellationToken);

            var employeeIds = rows
                .Where(item => item.HumanCapitalEmployeeId.HasValue)
                .Select(item => item.HumanCapitalEmployeeId!.Value)
                .Distinct()
                .ToList();

            var employeeMap = await _context.HumanCapitalEmployees
                .AsNoTracking()
                .Where(item => employeeIds.Contains(item.Id) && item.CurrentStatus == "فعال")
                .ToDictionaryAsync(item => item.Id, cancellationToken);

            if (employeeMap.Count != employeeIds.Count)
            {
                await transaction.RollbackAsync(cancellationToken);
                return BadRequest(new { success = false, message = "برخی پرسنل انتخابی معتبر یا فعال نیستند." });
            }

            var payrollItems = rows.Select(item =>
            {
                var hr = employeeMap[item.HumanCapitalEmployeeId!.Value];
                var lockedEmployeeName = hr.FullName.Trim();
                var lockedBaseSalary = hr.CurrentSalary;

                var allowance = item.Allowance;
                var overtime = item.Overtime;
                var tax = item.Tax;
                var insuranceDeduction = Math.Round((lockedBaseSalary + allowance + overtime) * 0.07m, 2);
                var netPayable = Math.Round((lockedBaseSalary + allowance + overtime) - insuranceDeduction - tax, 2);

                return new PayrollItem
                {
                    PayrollListId = payroll.Id,
                    HumanCapitalEmployeeId = item.HumanCapitalEmployeeId,
                    EmployeeName = lockedEmployeeName,
                    BaseSalary = lockedBaseSalary,
                    Allowance = allowance,
                    Overtime = overtime,
                    InsuranceDeduction = insuranceDeduction,
                    Tax = tax,
                    NetPayable = netPayable
                };
            });

            _context.PayrollItems.AddRange(payrollItems);
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Json(new
            {
                success = true,
                message = request.Finalize ? "لیست حقوق با موفقیت نهایی شد." : "لیست حقوق با موفقیت ذخیره شد.",
                redirectUrl = Url.Action(nameof(Index))
            });
        }

        [HttpGet]
        public IActionResult ExportExcelPlaceholder(int month, int year)
        {
            return Json(new
            {
                success = true,
                message = $"خروجی اکسل برای {year}/{month:00} در نسخه بعدی فعال می‌شود."
            });
        }

        [HttpGet]
        public IActionResult ExportPdfPlaceholder(int month, int year)
        {
            return Json(new
            {
                success = true,
                message = $"خروجی PDF برای {year}/{month:00} در نسخه بعدی فعال می‌شود."
            });
        }

        private static int GetCurrentSolarYear(DateTime dateTime)
        {
            var persianCalendar = new PersianCalendar();
            return persianCalendar.GetYear(dateTime);
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
        public async Task<IActionResult> GetEmployeeById(int id, CancellationToken cancellationToken)
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
                .FirstOrDefaultAsync(cancellationToken);

            if (employee == null)
            {
                return NotFound();
            }

            var latestPayrollItem = await _context.PayrollItems
                .AsNoTracking()
                .Where(item => item.HumanCapitalEmployeeId == id)
                .OrderByDescending(item => item.Id)
                .Select(item => new
                {
                    allowance = item.Allowance,
                    overtime = item.Overtime,
                    tax = item.Tax
                })
                .FirstOrDefaultAsync(cancellationToken);

            return Json(new
            {
                employee.id,
                employee.personnelCode,
                employee.fullName,
                employee.positionTitle,
                employee.currentSalary,
                employee.hireDate,
                employee.hireDateShamsi,
                allowance = latestPayrollItem?.allowance ?? 0m,
                overtime = latestPayrollItem?.overtime ?? 0m,
                tax = latestPayrollItem?.tax ?? 0m
            });
        }

        private static string ToSolarDate(DateTime dateTime)
        {
            var persianCalendar = new PersianCalendar();
            return $"{persianCalendar.GetYear(dateTime):0000}/{persianCalendar.GetMonth(dateTime):00}/{persianCalendar.GetDayOfMonth(dateTime):00}";
        }
    }
}
