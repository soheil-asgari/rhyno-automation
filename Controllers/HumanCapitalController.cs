using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Data;
using OfficeAutomation.Models;

namespace OfficeAutomation.Controllers
{
    [Authorize]
    public class HumanCapitalController : Controller
    {
        private readonly ApplicationDbContext _context;

        private static readonly string[] DefaultEmploymentTypes =
        [
            "تمام‌وقت",
            "پاره‌وقت",
            "پروژه‌ای",
            "قراردادی",
            "کارآموز"
        ];

        private static readonly string[] DefaultStatuses =
        [
            "فعال",
            "تعدیل شده",
            "ترک کار",
            "پایان خدمت"
        ];

        public HumanCapitalController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(HumanCapitalIndexVM filter)
        {
            var query = _context.HumanCapitalEmployees
                .AsNoTracking()
                .Include(employee => employee.Department)
                .AsQueryable();

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

            if (filter.DepartmentId.HasValue)
            {
                query = query.Where(employee => employee.DepartmentId == filter.DepartmentId.Value);
            }

            var fullListQuery = _context.HumanCapitalEmployees.AsNoTracking();

            filter.TotalCount = await fullListQuery.CountAsync();
            filter.ActiveCount = await fullListQuery.CountAsync(employee => employee.CurrentStatus == "فعال");
            filter.SeparatedCount = await fullListQuery.CountAsync(employee => employee.CurrentStatus != "فعال");
            filter.FilteredCount = await query.CountAsync();

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
        public async Task<IActionResult> Create(HumanCapitalCreateVM model)
        {
            await ValidateEmployeeUniquenessAsync(model.PersonnelCode, model.NationalCode);
            ValidateEmployeeDates(model.BirthDate, model.HireDate, model.ContractEndDate);

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
                PromotionTitle = "ثبت اولیه",
                Reason = "ثبت حقوق اولیه در زمان استخدام",
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
                CurrentStatus = employee.CurrentStatus,
                PhoneNumber = employee.PhoneNumber,
                Email = employee.Email,
                Address = employee.Address,
                Notes = employee.Notes
            };

            await PopulateCreateEditOptionsAsync(model);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, HumanCapitalEditVM model)
        {
            if (id != model.Id)
            {
                return NotFound();
            }

            var employee = await _context.HumanCapitalEmployees
                .FirstOrDefaultAsync(item => item.Id == id);

            if (employee == null)
            {
                return NotFound();
            }

            await ValidateEmployeeUniquenessAsync(model.PersonnelCode, model.NationalCode, id);
            ValidateEmployeeDates(model.BirthDate, model.HireDate, model.ContractEndDate);

            if (!ModelState.IsValid)
            {
                await PopulateCreateEditOptionsAsync(model);
                return View(model);
            }

            var now = DateTime.Now;
            var previousSalary = employee.CurrentSalary;
            var previousStatus = employee.CurrentStatus;

            await using var transaction = await _context.Database.BeginTransactionAsync();

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
            employee.CurrentStatus = model.CurrentStatus.Trim();
            employee.PhoneNumber = model.PhoneNumber?.Trim();
            employee.Email = model.Email?.Trim();
            employee.Address = model.Address?.Trim();
            employee.Notes = model.Notes?.Trim();
            employee.UpdatedAt = now;

            if (previousSalary != model.CurrentSalary)
            {
                _context.HumanCapitalSalaryHistories.Add(new HumanCapitalSalaryHistory
                {
                    EmployeeId = employee.Id,
                    EffectiveDate = now.Date,
                    PreviousSalary = previousSalary,
                    NewSalary = model.CurrentSalary,
                    PromotionTitle = "ویرایش پرونده",
                    Reason = "اصلاح حقوق در فرآیند ویرایش پرونده پرسنلی",
                    CreatedAt = now
                });
            }

            if (!string.Equals(previousStatus, model.CurrentStatus.Trim(), StringComparison.Ordinal))
            {
                _context.HumanCapitalStatusHistories.Add(new HumanCapitalStatusHistory
                {
                    EmployeeId = employee.Id,
                    StatusType = model.CurrentStatus.Trim(),
                    EffectiveDate = now.Date,
                    Description = "تغییر وضعیت از طریق ویرایش پرونده",
                    CreatedAt = now
                });
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return RedirectToAction(nameof(Details), new { id = employee.Id });
        }

        public async Task<IActionResult> Details(int id)
        {
            var model = await BuildDetailsViewModelAsync(id);
            if (model == null)
            {
                return NotFound();
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddSalaryIncrease([Bind(Prefix = "SalaryIncrease")] HumanCapitalSalaryIncreaseVM model)
        {
            var employee = await _context.HumanCapitalEmployees
                .FirstOrDefaultAsync(item => item.Id == model.EmployeeId);

            if (employee == null)
            {
                return NotFound();
            }

            if (model.NewSalary <= employee.CurrentSalary)
            {
                ModelState.AddModelError("SalaryIncrease.NewSalary", "حقوق جدید باید بیشتر از حقوق فعلی باشد.");
            }

            if (!ModelState.IsValid)
            {
                var invalidVm = await BuildDetailsViewModelAsync(model.EmployeeId, model, null);
                if (invalidVm == null)
                {
                    return NotFound();
                }

                return View("Details", invalidVm);
            }

            var now = DateTime.Now;
            var previousSalary = employee.CurrentSalary;

            await using var transaction = await _context.Database.BeginTransactionAsync();

            employee.CurrentSalary = model.NewSalary;
            employee.UpdatedAt = now;

            _context.HumanCapitalSalaryHistories.Add(new HumanCapitalSalaryHistory
            {
                EmployeeId = model.EmployeeId,
                EffectiveDate = model.EffectiveDate.Date,
                PreviousSalary = previousSalary,
                NewSalary = model.NewSalary,
                PromotionTitle = model.PromotionTitle?.Trim(),
                Reason = model.Reason.Trim(),
                CreatedAt = now
            });

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return RedirectToAction(nameof(Details), new { id = model.EmployeeId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddStatusChange([Bind(Prefix = "StatusChange")] HumanCapitalStatusChangeVM model)
        {
            var employee = await _context.HumanCapitalEmployees
                .FirstOrDefaultAsync(item => item.Id == model.EmployeeId);

            if (employee == null)
            {
                return NotFound();
            }

            if (!HumanCapitalProcessTypes.All.Contains(model.StatusType))
            {
                ModelState.AddModelError("StatusChange.StatusType", "نوع فرآیند انتخاب‌شده معتبر نیست.");
            }

            if (RequiresExitReason(model.StatusType) && string.IsNullOrWhiteSpace(model.ExitReason))
            {
                ModelState.AddModelError("StatusChange.ExitReason", "برای این فرآیند، دلیل خروج الزامی است.");
            }

            if (!ModelState.IsValid)
            {
                var invalidVm = await BuildDetailsViewModelAsync(model.EmployeeId, null, model);
                if (invalidVm == null)
                {
                    return NotFound();
                }

                return View("Details", invalidVm);
            }

            var now = DateTime.Now;

            await using var transaction = await _context.Database.BeginTransactionAsync();

            employee.CurrentStatus = MapCurrentStatus(model.StatusType);
            employee.UpdatedAt = now;

            _context.HumanCapitalStatusHistories.Add(new HumanCapitalStatusHistory
            {
                EmployeeId = model.EmployeeId,
                StatusType = model.StatusType,
                EffectiveDate = model.EffectiveDate.Date,
                ReferenceNumber = model.ReferenceNumber?.Trim(),
                Description = model.Description.Trim(),
                ExitReason = model.ExitReason?.Trim(),
                CreatedAt = now
            });

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return RedirectToAction(nameof(Details), new { id = model.EmployeeId });
        }

        private async Task<HumanCapitalDetailsVM?> BuildDetailsViewModelAsync(
            int employeeId,
            HumanCapitalSalaryIncreaseVM? salaryDraft = null,
            HumanCapitalStatusChangeVM? statusDraft = null)
        {
            var employee = await _context.HumanCapitalEmployees
                .AsNoTracking()
                .Include(item => item.Department)
                .Include(item => item.SalaryHistories)
                .Include(item => item.StatusHistories)
                .FirstOrDefaultAsync(item => item.Id == employeeId);

            if (employee == null)
            {
                return null;
            }

            var salaryVm = salaryDraft ?? new HumanCapitalSalaryIncreaseVM
            {
                EmployeeId = employee.Id,
                EffectiveDate = DateTime.Today,
                NewSalary = employee.CurrentSalary
            };
            salaryVm.EmployeeId = employee.Id;

            var statusVm = statusDraft ?? new HumanCapitalStatusChangeVM
            {
                EmployeeId = employee.Id,
                EffectiveDate = DateTime.Today,
                StatusType = HumanCapitalProcessTypes.Termination
            };
            statusVm.EmployeeId = employee.Id;

            return new HumanCapitalDetailsVM
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
                SalaryIncrease = salaryVm,
                StatusChange = statusVm,
                ProcessTypeOptions = BuildProcessTypeOptions(statusVm.StatusType),
                SalaryHistories = employee.SalaryHistories
                    .OrderByDescending(item => item.EffectiveDate)
                    .ThenByDescending(item => item.CreatedAt)
                    .Select(item => new HumanCapitalSalaryHistoryVM
                    {
                        EffectiveDate = item.EffectiveDate,
                        PreviousSalary = item.PreviousSalary,
                        NewSalary = item.NewSalary,
                        PromotionTitle = item.PromotionTitle,
                        Reason = item.Reason,
                        CreatedAt = item.CreatedAt
                    })
                    .ToList(),
                StatusHistories = employee.StatusHistories
                    .OrderByDescending(item => item.EffectiveDate)
                    .ThenByDescending(item => item.CreatedAt)
                    .Select(item => new HumanCapitalStatusHistoryVM
                    {
                        StatusType = item.StatusType,
                        EffectiveDate = item.EffectiveDate,
                        ReferenceNumber = item.ReferenceNumber,
                        Description = item.Description,
                        ExitReason = item.ExitReason,
                        CreatedAt = item.CreatedAt
                    })
                    .ToList()
            };
        }

        private async Task<List<SelectListItem>> BuildDepartmentOptionsAsync(int? selectedDepartmentId = null)
        {
            return await _context.Departments
                .AsNoTracking()
                .OrderBy(department => department.Name)
                .Select(department => new SelectListItem
                {
                    Value = department.Id.ToString(),
                    Text = department.Name,
                    Selected = selectedDepartmentId.HasValue && selectedDepartmentId.Value == department.Id
                })
                .ToListAsync();
        }

        private async Task<List<string>> BuildStatusOptionsAsync()
        {
            var dbStatuses = await _context.HumanCapitalEmployees
                .AsNoTracking()
                .Where(employee => !string.IsNullOrWhiteSpace(employee.CurrentStatus))
                .Select(employee => employee.CurrentStatus)
                .Distinct()
                .ToListAsync();

            return DefaultStatuses
                .Concat(dbStatuses)
                .Where(status => !string.IsNullOrWhiteSpace(status))
                .Select(status => status.Trim())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(status => status)
                .ToList();
        }

        private async Task PopulateCreateEditOptionsAsync(HumanCapitalBaseUpsertVM model)
        {
            if (string.IsNullOrWhiteSpace(model.EmploymentType))
            {
                model.EmploymentType = DefaultEmploymentTypes[0];
            }

            model.DepartmentOptions = await BuildDepartmentOptionsAsync(model.DepartmentId);
            model.EmploymentTypeOptions = DefaultEmploymentTypes
                .Distinct()
                .Select(type => new SelectListItem
                {
                    Value = type,
                    Text = type,
                    Selected = string.Equals(type, model.EmploymentType, StringComparison.Ordinal)
                })
                .ToList();
        }

        private async Task ValidateEmployeeUniquenessAsync(string personnelCode, string nationalCode, int? employeeId = null)
        {
            var personnelCodeNormalized = personnelCode.Trim();
            var nationalCodeNormalized = nationalCode.Trim();

            var duplicatePersonnelCode = await _context.HumanCapitalEmployees
                .AsNoTracking()
                .AnyAsync(employee =>
                    employee.PersonnelCode == personnelCodeNormalized &&
                    (!employeeId.HasValue || employee.Id != employeeId.Value));

            if (duplicatePersonnelCode)
            {
                ModelState.AddModelError(nameof(HumanCapitalCreateVM.PersonnelCode), "این کد پرسنلی قبلاً ثبت شده است.");
            }

            var duplicateNationalCode = await _context.HumanCapitalEmployees
                .AsNoTracking()
                .AnyAsync(employee =>
                    employee.NationalCode == nationalCodeNormalized &&
                    (!employeeId.HasValue || employee.Id != employeeId.Value));

            if (duplicateNationalCode)
            {
                ModelState.AddModelError(nameof(HumanCapitalCreateVM.NationalCode), "این کد ملی قبلاً ثبت شده است.");
            }
        }

        private void ValidateEmployeeDates(DateTime birthDate, DateTime hireDate, DateTime? contractEndDate)
        {
            if (birthDate.Date >= hireDate.Date)
            {
                ModelState.AddModelError(nameof(HumanCapitalCreateVM.BirthDate), "تاریخ تولد باید قبل از تاریخ استخدام باشد.");
            }

            if (contractEndDate.HasValue && contractEndDate.Value.Date < hireDate.Date)
            {
                ModelState.AddModelError(nameof(HumanCapitalCreateVM.ContractEndDate), "تاریخ پایان قرارداد نمی‌تواند قبل از تاریخ استخدام باشد.");
            }
        }

        private static string MapCurrentStatus(string statusType)
        {
            return statusType switch
            {
                HumanCapitalProcessTypes.Termination => "تعدیل شده",
                HumanCapitalProcessTypes.Resignation => "ترک کار",
                HumanCapitalProcessTypes.EndOfService => "پایان خدمت",
                _ => "فعال"
            };
        }

        private static bool RequiresExitReason(string statusType)
        {
            return statusType == HumanCapitalProcessTypes.Termination ||
                   statusType == HumanCapitalProcessTypes.Resignation ||
                   statusType == HumanCapitalProcessTypes.EndOfService;
        }

        private static List<SelectListItem> BuildProcessTypeOptions(string selectedStatusType)
        {
            return HumanCapitalProcessTypes.All
                .Select(type => new SelectListItem
                {
                    Value = type,
                    Text = type,
                    Selected = string.Equals(type, selectedStatusType, StringComparison.Ordinal)
                })
                .ToList();
        }
    }
}
