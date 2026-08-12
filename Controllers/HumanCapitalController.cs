using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Modules.Office.Infrastructure.Persistence;
using OfficeAutomation.Models;
using OfficeAutomation.Services.Security;

namespace OfficeAutomation.Controllers
{
    [Authorize]
    [PermissionAuthorize("HR.View")]
    public class HumanCapitalController : Controller
    {
        private readonly OfficeDbContext _context;
        private readonly IDataIsolationService _dataIsolationService;
        private readonly ISecurityFieldMaskingService _securityFieldMaskingService;

        private static readonly string[] DefaultEmploymentTypes =
        [
            "تمام وقت",
            "پاره وقت",
            "پروژه‌ای",
            "کارآموزی",
            "نیروی دورکار"
        ];

        private static readonly string[] DefaultStatuses =
        [
            "فعال",
            "در انتظار تایید",
            "مرخصی موقت",
            "اخراج شده"
        ];

        public HumanCapitalController(OfficeDbContext context, IDataIsolationService dataIsolationService, ISecurityFieldMaskingService securityFieldMaskingService)
        {
            _context = context;
            _dataIsolationService = dataIsolationService;
            _securityFieldMaskingService = securityFieldMaskingService;
        }

        public async Task<IActionResult> Index(HumanCapitalIndexVM filter)
        {
            var query = await _dataIsolationService.ApplyDepartmentScopeAsync(_context.HumanCapitalEmployees
                .AsNoTracking()
                .Include(employee => employee.Department)
                .AsQueryable());

            if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
            {
                var searchTerm = filter.SearchTerm.Trim();
                query = query.Where(employee =>
                    employee.FullName.Contains(searchTerm) ||
                    employee.PersonnelCode.Contains(searchTerm) ||
                    employee.NationalCode.Contains(searchTerm) ||
                    employee.PositionTitle.Contains(searchTerm));
            }

            if (!string.IsNullOrWhiteSpace(filter.Status))
            {
                var status = filter.Status.Trim();
                query = query.Where(employee => employee.CurrentStatus == status);
            }

            if (string.Equals(filter.WorkflowView, "active", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(employee => employee.CurrentStatus == "فعال");
            }
            else if (string.Equals(filter.WorkflowView, "separated", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(employee => employee.CurrentStatus != "فعال");
            }

            if (filter.DepartmentId.HasValue)
            {
                query = query.Where(employee => employee.DepartmentId == filter.DepartmentId.Value);
            }

            var fullListQuery = await _dataIsolationService.ApplyDepartmentScopeAsync(_context.HumanCapitalEmployees.AsNoTracking());

            filter.TotalCount = await fullListQuery.CountAsync();
            filter.ActiveCount = await fullListQuery.CountAsync(employee => employee.CurrentStatus == "فعال");
            filter.SeparatedCount = await fullListQuery.CountAsync(employee => employee.CurrentStatus != "فعال");
            filter.FilteredCount = await query.CountAsync();
            filter.WorkflowView ??= "all";

            filter.Items = await query
                .OrderByDescending(employee => employee.UpdatedAt)
                .Select(employee => new HumanCapitalIndexItemVM
                {
                    Id = employee.Id,
                    PersonnelCode = employee.PersonnelCode,
                    FullName = employee.FullName,
                    DepartmentName = employee.Department != null ? employee.Department.Name : null,
                    PositionTitle = employee.PositionTitle,
                    HireDate = employee.HireDate,
                    CurrentSalary = employee.CurrentSalary,
                    CurrentStatus = employee.CurrentStatus,
                    SalaryChangeCount = employee.SalaryHistories.Count,
                    LatestStatusDate = employee.StatusHistories
                        .OrderByDescending(status => status.EffectiveDate)
                        .Select(status => (DateTime?)status.EffectiveDate)
                        .FirstOrDefault()
                })
                .ToListAsync();

            filter.DepartmentOptions = await BuildDepartmentOptionsAsync(filter.DepartmentId);
            filter.StatusOptions = await BuildStatusOptionsAsync();
            await _securityFieldMaskingService.MaskHumanCapitalIndexAsync(filter);

            return View(filter);
        }

        public async Task<IActionResult> Create()
        {
            var model = new HumanCapitalCreateVM();
            await PopulateCreateEditOptionsAsync(model);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("HR.Create")]
        public async Task<IActionResult> Create(HumanCapitalCreateVM model)
        {
            await ValidateEmployeeUniquenessAsync(model.PersonnelCode, model.NationalCode);
            ValidateEmployeeDates(model.BirthDate, model.HireDate, model.ContractEndDate);
            ValidateInitialStatus(model.InitialStatusDate, model.HireDate, model.InitialStatusDescription);

            if (!ModelState.IsValid)
            {
                await PopulateCreateEditOptionsAsync(model);
                return View(model);
            }

            var now = DateTime.Now;
            var employee = new HumanCapitalEmployee
            {
                PersonnelCode = model.PersonnelCode.Trim(),
                FullName = model.FullName.Trim(),
                NationalCode = model.NationalCode.Trim(),
                BirthDate = model.BirthDate.Date,
                HireDate = model.HireDate.Date,
                ContractEndDate = model.ContractEndDate?.Date,
                OnboardingCompleted = model.OnboardingCompleted,
                DepartmentId = model.DepartmentId,
                PositionTitle = model.PositionTitle.Trim(),
                EmploymentType = model.EmploymentType.Trim(),
                CurrentSalary = model.CurrentSalary,
                CurrentStatus = "فعال",
                PhoneNumber = model.PhoneNumber?.Trim(),
                Email = model.Email?.Trim(),
                Address = model.Address?.Trim(),
                Notes = model.Notes?.Trim(),
                CreatedAt = now,
                UpdatedAt = now
            };

            await using var transaction = await _context.Database.BeginTransactionAsync();

            _context.HumanCapitalEmployees.Add(employee);
            await _context.SaveChangesAsync();

            _context.HumanCapitalSalaryHistories.Add(new HumanCapitalSalaryHistory
            {
                EmployeeId = employee.Id,
                EffectiveDate = employee.HireDate,
                PreviousSalary = 0,
                NewSalary = employee.CurrentSalary,
                PromotionTitle = "اولین حقوق",
                Reason = "اولیه صدور استخدام",
                CreatedAt = now
            });

            _context.HumanCapitalStatusHistories.Add(new HumanCapitalStatusHistory
            {
                EmployeeId = employee.Id,
                StatusType = HumanCapitalProcessTypes.Recruitment,
                EffectiveDate = model.InitialStatusDate.Date,
                ReferenceNumber = model.InitialReferenceNumber?.Trim(),
                Description = model.InitialStatusDescription.Trim(),
                CreatedAt = now
            });

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return RedirectToAction(nameof(Details), new { id = employee.Id });
        }

        public async Task<IActionResult> Edit(int id)
        {
            var employee = await _context.HumanCapitalEmployees
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == id);

            if (employee == null)
            {
                return NotFound();
            }

            var model = new HumanCapitalEditVM
            {
                Id = employee.Id,
                PersonnelCode = employee.PersonnelCode,
                FullName = employee.FullName,
                NationalCode = employee.NationalCode,
                BirthDate = employee.BirthDate,
                HireDate = employee.HireDate,
                ContractEndDate = employee.ContractEndDate,
                OnboardingCompleted = employee.OnboardingCompleted,
                DepartmentId = employee.DepartmentId,
                PositionTitle = employee.PositionTitle,
                EmploymentType = employee.EmploymentType,
                CurrentSalary = employee.CurrentSalary,
                PhoneNumber = employee.PhoneNumber,
                Email = employee.Email,
                Address = employee.Address,
                Notes = employee.Notes,
                CurrentStatus = employee.CurrentStatus
            };

            await PopulateCreateEditOptionsAsync(model);

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("HR.Edit")]
        public async Task<IActionResult> Edit(HumanCapitalEditVM model)
        {
            await ValidateEmployeeUniquenessAsync(model.PersonnelCode, model.NationalCode, model.Id);
            ValidateEmployeeDates(model.BirthDate, model.HireDate, model.ContractEndDate);

            if (!ModelState.IsValid)
            {
                await PopulateCreateEditOptionsAsync(model);
                return View(model);
            }

            var now = DateTime.Now;
            var employee = await _context.HumanCapitalEmployees.FindAsync(model.Id);

            if (employee == null)
            {
                return NotFound();
            }

            var salaryChanged = employee.CurrentSalary != model.CurrentSalary;
            var statusChanged = employee.CurrentStatus != model.CurrentStatus;

            employee.PersonnelCode = model.PersonnelCode.Trim();
            employee.FullName = model.FullName.Trim();
            employee.NationalCode = model.NationalCode.Trim();
            employee.BirthDate = model.BirthDate.Date;
            employee.HireDate = model.HireDate.Date;
            employee.ContractEndDate = model.ContractEndDate?.Date;
            employee.OnboardingCompleted = model.OnboardingCompleted;
            employee.DepartmentId = model.DepartmentId;
            employee.PositionTitle = model.PositionTitle.Trim();
            employee.EmploymentType = model.EmploymentType.Trim();
            employee.CurrentSalary = model.CurrentSalary;
            employee.CurrentStatus = model.CurrentStatus;
            employee.PhoneNumber = model.PhoneNumber?.Trim();
            employee.Email = model.Email?.Trim();
            employee.Address = model.Address?.Trim();
            employee.Notes = model.Notes?.Trim();
            employee.UpdatedAt = now;

            await using var transaction = await _context.Database.BeginTransactionAsync();

            await _context.SaveChangesAsync();

            if (salaryChanged)
            {
                _context.HumanCapitalSalaryHistories.Add(new HumanCapitalSalaryHistory
                {
                    EmployeeId = employee.Id,
                    EffectiveDate = now,
                    PreviousSalary = employee.CurrentSalary,
                    NewSalary = model.CurrentSalary,
                    PromotionTitle = "تغییر حقوق",
                    Reason = "تغییر حقوق",
                    CreatedAt = now
                });
            }

            if (statusChanged)
            {
                _context.HumanCapitalStatusHistories.Add(new HumanCapitalStatusHistory
                {
                    EmployeeId = employee.Id,
                    StatusType = "تغییر وضعیت",
                    EffectiveDate = now,
                    Description = "تغییر وضعیت",
                    CreatedAt = now
                });
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return RedirectToAction(nameof(Details), new { id = employee.Id });
        }

        public async Task<IActionResult> Details(int id)
        {
            var employee = await _context.HumanCapitalEmployees
                .AsNoTracking()
                .Include(e => e.Department)
                .Include(e => e.SalaryHistories)
                .Include(e => e.StatusHistories)
                .FirstOrDefaultAsync(item => item.Id == id);

            if (employee == null)
            {
                return NotFound();
            }

            var vm = new HumanCapitalDetailsVM
            {
                Id = employee.Id,
                PersonnelCode = employee.PersonnelCode,
                FullName = employee.FullName,
                NationalCode = employee.NationalCode,
                BirthDate = employee.BirthDate,
                HireDate = employee.HireDate,
                ContractEndDate = employee.ContractEndDate,
                OnboardingCompleted = employee.OnboardingCompleted,
                DepartmentName = employee.Department?.Name,
                PositionTitle = employee.PositionTitle,
                EmploymentType = employee.EmploymentType,
                CurrentSalary = employee.CurrentSalary,
                CurrentStatus = employee.CurrentStatus,
                PhoneNumber = employee.PhoneNumber,
                Email = employee.Email,
                Address = employee.Address,
                Notes = employee.Notes,
                SalaryHistories = employee.SalaryHistories
                    .OrderByDescending(sh => sh.EffectiveDate)
                    .Select(sh => new HumanCapitalSalaryHistoryVM
                    {
                        EffectiveDate = sh.EffectiveDate,
                        PreviousSalary = sh.PreviousSalary,
                        NewSalary = sh.NewSalary,
                        PromotionTitle = sh.PromotionTitle,
                        Reason = sh.Reason,
                        CreatedAt = sh.CreatedAt
                    })
                    .ToList(),
                StatusHistories = employee.StatusHistories
                    .OrderByDescending(sh => sh.EffectiveDate)
                    .Select(sh => new HumanCapitalStatusHistoryVM
                    {
                        StatusType = sh.StatusType,
                        EffectiveDate = sh.EffectiveDate,
                        ReferenceNumber = sh.ReferenceNumber,
                        Description = sh.Description,
                        CreatedAt = sh.CreatedAt
                    })
                    .ToList()
            };

            await _securityFieldMaskingService.MaskHumanCapitalDetailsAsync(vm);

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("HR.Delete")]
        public async Task<IActionResult> Delete(int id)
        {
            var employee = await _context.HumanCapitalEmployees.FindAsync(id);

            if (employee == null)
            {
                return NotFound();
            }

            _context.HumanCapitalEmployees.Remove(employee);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        private async Task PopulateCreateEditOptionsAsync(HumanCapitalBaseUpsertVM model)
        {
            var departments = await _dataIsolationService.ApplyDepartmentScopeAsync(_context.Departments.AsNoTracking());
            model.DepartmentOptions = departments.Select(d => new SelectListItem
            {
                Text = d.Name,
                Value = d.Id.ToString()
            }).ToList();

            model.EmploymentTypeOptions = DefaultEmploymentTypes
                .Distinct()
                .Select(type => new SelectListItem
                {
                    Text = type,
                    Value = type,
                    Selected = string.Equals(type, model.EmploymentType, StringComparison.Ordinal)
                })
                .ToList();
        }

        private async Task<List<SelectListItem>> BuildDepartmentOptionsAsync(int? selectedDepartmentId)
        {
            var departments = await _dataIsolationService.ApplyDepartmentScopeAsync(_context.Departments.AsNoTracking());
            var options = departments.Select(d => new SelectListItem
            {
                Text = d.Name,
                Value = d.Id.ToString(),
                Selected = d.Id == selectedDepartmentId
            }).ToList();
            options.Insert(0, new SelectListItem { Value = "", Text = "--- همه دپارتمان‌ها ---" });
            return options;
        }

        private async Task<List<SelectListItem>> BuildStatusOptionsAsync()
        {
            var statuses = new List<SelectListItem>
            {
                new SelectListItem { Value = "", Text = "--- همه وضعیت‌ها ---" }
            };
            statuses.AddRange(DefaultStatuses.Select(s => new SelectListItem { Text = s, Value = s }).ToList());
            return statuses;
        }

        private async Task ValidateEmployeeUniquenessAsync(string personnelCode, string nationalCode, int? excludeId = null)
        {
            var existingEmployee = await _context.HumanCapitalEmployees
                .AsNoTracking()
                .FirstOrDefaultAsync(e =>
                    (e.PersonnelCode.Trim().Equals(personnelCode.Trim(), StringComparison.OrdinalIgnoreCase) ||
                     e.NationalCode.Trim().Equals(nationalCode.Trim(), StringComparison.OrdinalIgnoreCase)) &&
                    (!excludeId.HasValue || e.Id != excludeId));

            if (existingEmployee != null)
            {
                if (existingEmployee.PersonnelCode.Trim().Equals(personnelCode.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    ModelState.AddModelError("PersonnelCode", "کد پرسنلی تکراری است.");
                }
                if (existingEmployee.NationalCode.Trim().Equals(nationalCode.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    ModelState.AddModelError("NationalCode", "کد ملی تکراری است.");
                }
            }
        }

        private void ValidateEmployeeDates(DateTime? birthDate, DateTime? hireDate, DateTime? contractEndDate)
        {
            if (birthDate.HasValue && hireDate.HasValue && birthDate.Value.Date >= hireDate.Value.Date)
            {
                ModelState.AddModelError("BirthDate", "تاریخ تولد باید قبل از تاریخ استخدام باشد.");
            }

            if (contractEndDate.HasValue && hireDate.HasValue && contractEndDate.Value.Date < hireDate.Value.Date)
            {
                ModelState.AddModelError("ContractEndDate", "تاریخ پایان قرارداد باید بعد از تاریخ استخدام باشد.");
            }
        }

        private void ValidateInitialStatus(DateTime initialStatusDate, DateTime hireDate, string initialStatusDescription)
        {
            if (initialStatusDate.Date < hireDate.Date)
            {
                ModelState.AddModelError("InitialStatusDate", "تاریخ شروع وضعیت اولیه باید بعد از تاریخ استخدام باشد.");
            }
        }
    }
}