using OfficeAutomation.Services.Licensing;
using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using OfficeAutomation.Modules.Office.Infrastructure.Persistence;
using OfficeAutomation.Models;
using OfficeAutomation.Services;
using OfficeAutomation.Services.Security;
using OfficeAutomation.Services.Tenancy;

namespace OfficeAutomation.Controllers
{
    [Authorize]
    [PermissionAuthorize("HR.View")]
    [RequiresModule(LicensableModules.HumanCapital)]
public class HumanCapitalController : Controller
    {
        private readonly OfficeDbContext _context;
        private readonly IDataIsolationService _dataIsolationService;
        private readonly ISecurityFieldMaskingService _securityFieldMaskingService;
        private readonly ICurrentDataAccessScope _dataAccessScope;
        private readonly ILogger<HumanCapitalController> _logger;
        private readonly IWebHostEnvironment _environment;
        private readonly IExportService _exportService;
        private readonly ITenantIsolationService _tenantIsolationService;

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

        private static readonly Dictionary<string, string> DepartmentPersianNames = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Administrative"] = "اداری",
            ["Financial"] = "مالی",
            ["Finance"] = "مالی",
            ["HR"] = "منابع انسانی",
            ["Human Resources"] = "منابع انسانی",
            ["Technical"] = "فنی و مهندسی",
            ["IT"] = "فناوری اطلاعات",
            ["Sales"] = "فروش",
            ["Marketing"] = "بازاریابی",
            ["Legal"] = "حقوقی",
            ["Operations"] = "عملیات",
            ["Logistics"] = "تدارکات",
            ["Warehouse"] = "انبار"
        };

        public HumanCapitalController(
            OfficeDbContext context,
            IDataIsolationService dataIsolationService,
            ISecurityFieldMaskingService securityFieldMaskingService,
            ICurrentDataAccessScope dataAccessScope,
            ILogger<HumanCapitalController> logger,
            IWebHostEnvironment environment,
            IExportService exportService,
            ITenantIsolationService tenantIsolationService)
        {
            _context = context;
            _dataIsolationService = dataIsolationService;
            _securityFieldMaskingService = securityFieldMaskingService;
            _dataAccessScope = dataAccessScope;
            _logger = logger;
            _environment = environment;
            _exportService = exportService;
            _tenantIsolationService = tenantIsolationService;
        }

        public async Task<IActionResult> Index(HumanCapitalIndexVM filter)
        {
            // Tenant isolation is enforced at the schema level (schema-per-tenant).
            // Do NOT apply department scope filtering here — it causes employees
            // created with a different (or null) DepartmentId to be hidden from
            // users who have a specific department assigned, even though the data
            // exists in the same tenant schema.
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

            if (!string.IsNullOrWhiteSpace(filter.EmploymentType))
            {
                var employmentType = filter.EmploymentType.Trim();
                query = query.Where(employee => employee.EmploymentType == employmentType);
            }

            // Use unfiltered query for KPI summary cards — same reasoning as above.
            var fullListQuery = _context.HumanCapitalEmployees.AsNoTracking();

            filter.TotalCount = await fullListQuery.CountAsync();
            filter.ActiveCount = await fullListQuery.CountAsync(employee => employee.CurrentStatus == "فعال");
            filter.SeparatedCount = await fullListQuery.CountAsync(employee => employee.CurrentStatus != "فعال");
            filter.PendingOnboardingCount = await fullListQuery.CountAsync(employee => !employee.OnboardingCompleted);
            filter.OnLeaveCount = await fullListQuery.CountAsync(employee => employee.CurrentStatus == DefaultStatuses[2]);
            filter.TotalMonthlyPayroll = await fullListQuery.SumAsync(employee => employee.CurrentSalary);
            filter.AverageSalary = filter.TotalCount > 0 ? filter.TotalMonthlyPayroll / filter.TotalCount : 0;
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
                    NationalCode = employee.NationalCode,
                    PhoneNumber = employee.PhoneNumber,
                    EmploymentType = employee.EmploymentType,
                    OnboardingCompleted = employee.OnboardingCompleted,
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
            filter.EmploymentTypeOptions = DefaultEmploymentTypes.ToList();
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
            try
            {
                if (model == null)
                {
                    model = new HumanCapitalCreateVM();
                    await PopulateCreateEditOptionsAsync(model);
                    return View(model);
                }

                BindJalaliDateFields(model);

                var birthDate = ParseJalaliToGregorian(model.BirthDate);
                var hireDate = ParseJalaliToGregorian(model.HireDate);
                var contractEndDate = ParseJalaliToGregorian(model.ContractEndDate);
                var initialStatusDate = ParseJalaliToGregorian(model.InitialStatusDate);

                await ValidateEmployeeUniquenessAsync(model.PersonnelCode, model.NationalCode);
                ValidateEmployeeDates(birthDate, hireDate, contractEndDate);
                ValidateInitialStatus(initialStatusDate ?? DateTime.MinValue, hireDate ?? DateTime.MinValue);

                if (!ModelState.IsValid)
                {
                    await PopulateCreateEditOptionsAsync(model);
                    if (IsJsonRequest())
                    {
                        return BadRequest(BuildValidationErrorResponse());
                    }
                    return View(model);
                }

                var now = DateTime.Now;
                var effectiveDepartmentId = ResolveDepartmentId(model.DepartmentId);
                if (!_dataAccessScope.HasGlobalAccess && !effectiveDepartmentId.HasValue)
                {
                    ModelState.AddModelError("DepartmentId", "دپارتمان شما مشخص نشده است. برای ثبت کارمند باید به یک دپارتمان تخصیص یافته باشید.");
                    await PopulateCreateEditOptionsAsync(model);
                    if (IsJsonRequest())
                    {
                        return BadRequest(BuildValidationErrorResponse());
                    }
                    return View(model);
                }

                if (!birthDate.HasValue || !hireDate.HasValue || !initialStatusDate.HasValue)
                {
                    if (!birthDate.HasValue) ModelState.AddModelError("BirthDate", "تاریخ تولد معتبر نیست.");
                    if (!hireDate.HasValue) ModelState.AddModelError("HireDate", "تاریخ استخدام معتبر نیست.");
                    if (!initialStatusDate.HasValue) ModelState.AddModelError("InitialStatusDate", "تاریخ رخداد معتبر نیست.");
                    await PopulateCreateEditOptionsAsync(model);
                    if (IsJsonRequest())
                    {
                        return BadRequest(BuildValidationErrorResponse());
                    }
                    return View(model);
                }

                var employee = new HumanCapitalEmployee
                {
                    PersonnelCode = model.PersonnelCode?.Trim() ?? string.Empty,
                    FullName = model.FullName?.Trim() ?? string.Empty,
                    NationalCode = model.NationalCode?.Trim() ?? string.Empty,
                    BirthDate = birthDate.Value.Date,
                    HireDate = hireDate.Value.Date,
                    ContractEndDate = contractEndDate?.Date,
                    OnboardingCompleted = model.OnboardingCompleted,
                    DepartmentId = effectiveDepartmentId,
                    PositionTitle = model.PositionTitle?.Trim() ?? string.Empty,
                    EmploymentType = model.EmploymentType?.Trim() ?? string.Empty,
                    CurrentSalary = model.CurrentSalary,
                    CurrentStatus = "فعال",
                    PhoneNumber = model.PhoneNumber?.Trim(),
                    Email = model.Email?.Trim(),
                    Address = model.Address?.Trim(),
                    Notes = model.Notes?.Trim(),
                    CreatedAt = now,
                    UpdatedAt = now
                };

                try
                {
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
                        EffectiveDate = initialStatusDate.Value.Date,
                        ReferenceNumber = model.InitialReferenceNumber?.Trim(),
                        Description = model.InitialStatusDescription?.Trim() ?? string.Empty,
                        CreatedAt = now
                    });

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                }
                catch (DbUpdateException)
                {
                    ModelState.AddModelError(string.Empty, "ذخیره‌سازی ناموفق بود. لطفاً داده‌های ورودی را بررسی و دوباره تلاش کنید.");
                    await PopulateCreateEditOptionsAsync(model);
                    if (IsJsonRequest())
                    {
                        return BadRequest(BuildValidationErrorResponse());
                    }
                    return View(model);
                }

                if (IsJsonRequest())
                {
                    return Ok(new { redirectUrl = Url.Action(nameof(Details), new { id = employee.Id }) });
                }

                return RedirectToAction(nameof(Details), new { id = employee.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while creating human capital record.");
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    success = false,
                    message = _environment.IsDevelopment() ? ex.Message : "خطا در ذخیره‌سازی. لطفاً دوباره تلاش کنید یا صفحه را بازنشانی کنید."
                });
            }
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
                BirthDate = employee.BirthDate.ToString("yyyy-MM-dd"),
                HireDate = employee.HireDate.ToString("yyyy-MM-dd"),
                ContractEndDate = employee.ContractEndDate?.ToString("yyyy-MM-dd"),
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
            BindJalaliDateFields(model);

            var editBirthDate = ParseJalaliToGregorian(model.BirthDate);
            var editHireDate = ParseJalaliToGregorian(model.HireDate);
            var editContractEndDate = ParseJalaliToGregorian(model.ContractEndDate);

            await ValidateEmployeeUniquenessAsync(model.PersonnelCode, model.NationalCode, model.Id);
            ValidateEmployeeDates(editBirthDate, editHireDate, editContractEndDate);

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
            employee.BirthDate = ParseJalaliToGregorian(model.BirthDate) ?? employee.BirthDate;
            employee.HireDate = ParseJalaliToGregorian(model.HireDate) ?? employee.HireDate;
            employee.ContractEndDate = ParseJalaliToGregorian(model.ContractEndDate) ?? employee.ContractEndDate;
            employee.OnboardingCompleted = model.OnboardingCompleted;
            employee.DepartmentId = ResolveDepartmentId(model.DepartmentId);
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

        [HttpGet]
        public async Task<IActionResult> SearchEmployees(string? q)
        {
            var query = _context.HumanCapitalEmployees
                .AsNoTracking()
                .Where(e => e.CurrentStatus == "فعال");

            if (!string.IsNullOrWhiteSpace(q))
            {
                var searchTerm = q.Trim().ToLower();
                query = query.Where(e =>
                    e.FullName.ToLower().Contains(searchTerm) ||
                    e.PersonnelCode.ToLower().Contains(searchTerm) ||
                    e.NationalCode.ToLower().Contains(searchTerm));
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
                    hireDate = e.HireDate
                })
                .ToListAsync();

            return Json(employees);
        }

        // ── Human Capital Sub-Modules (enterprise workflow) ──────────

        // ── Contracts ─────────────────────────────────────────────
        public async Task<IActionResult> Contracts()
        {
            ViewData["Title"] = "احکام کارگزینی و قراردادها";
            ViewData["SectionTitle"] = "احکام کارگزینی و قراردادها";

            var contracts = await _context.HrContracts
                .AsNoTracking()
                .Include(c => c.Employee)
                .OrderByDescending(c => c.CreatedAt)
                .Select(c => new HrContractItemVM
                {
                    Id = c.Id,
                    EmployeeId = c.EmployeeId,
                    EmployeeName = c.Employee != null ? c.Employee.FullName : "—",
                    PersonnelCode = c.Employee != null ? c.Employee.PersonnelCode : "—",
                    ContractType = c.ContractType,
                    StartDate = c.StartDate,
                    EndDate = c.EndDate,
                    BaseSalary = c.BaseSalary,
                    IsActive = c.IsActive
                })
                .ToListAsync();

            var vm = new HrContractIndexVM
            {
                Items = contracts,
                TotalCount = contracts.Count,
                ActiveCount = contracts.Count(c => c.IsActive)
            };

            return View(vm);
        }

        public async Task<IActionResult> CreateContract()
        {
            var model = new HrContractCreateVM();
            await PopulateEmployeeOptionsAsync(model);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("HR.Create")]
        public async Task<IActionResult> CreateContract(HrContractCreateVM model)
        {
            if (model == null)
            {
                model = new HrContractCreateVM();
                await PopulateEmployeeOptionsAsync(model);
                return View(model);
            }

            var startDate = ParseJalaliToGregorian(model.StartDate);
            var endDate = ParseJalaliToGregorian(model.EndDate);

            if (!startDate.HasValue)
            {
                ModelState.AddModelError("StartDate", "تاریخ شروع معتبر نیست.");
            }

            if (!ModelState.IsValid)
            {
                await PopulateEmployeeOptionsAsync(model);
                return View(model);
            }

            var now = DateTime.Now;
            var contract = new HrContract
            {
                EmployeeId = model.EmployeeId,
                ContractType = model.ContractType?.Trim() ?? "تمام وقت",
                StartDate = startDate!.Value.Date,
                EndDate = endDate?.Date,
                BaseSalary = model.BaseSalary,
                IsActive = model.IsActive,
                Notes = model.Notes?.Trim(),
                TenantId = _tenantIsolationService.GetCurrent().TenantId ?? string.Empty,
                CreatedAt = now,
                UpdatedAt = now
            };

            _context.HrContracts.Add(contract);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Contracts));
        }

        // ── Leaves ────────────────────────────────────────────────
        public async Task<IActionResult> Leaves()
        {
            ViewData["Title"] = "مدیریت مرخصی و مأموریت";
            ViewData["SectionTitle"] = "مدیریت مرخصی و مأموریت";

            var leaves = await _context.HrLeaves
                .AsNoTracking()
                .Include(l => l.Employee)
                .OrderByDescending(l => l.CreatedAt)
                .Select(l => new HrLeaveItemVM
                {
                    Id = l.Id,
                    EmployeeId = l.EmployeeId,
                    EmployeeName = l.Employee != null ? l.Employee.FullName : "—",
                    PersonnelCode = l.Employee != null ? l.Employee.PersonnelCode : "—",
                    LeaveType = l.LeaveType,
                    StartDate = l.StartDate,
                    EndDate = l.EndDate,
                    Description = l.Description,
                    Status = l.Status
                })
                .ToListAsync();

            var vm = new HrLeaveIndexVM
            {
                Items = leaves,
                TotalCount = leaves.Count,
                PendingCount = leaves.Count(l => l.Status == "در انتظار"),
                ApprovedCount = leaves.Count(l => l.Status == "تایید شده")
            };

            return View(vm);
        }

        public async Task<IActionResult> CreateLeave()
        {
            var model = new HrLeaveCreateVM();
            await PopulateEmployeeOptionsForCreateAsync(model);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("HR.Create")]
        public async Task<IActionResult> CreateLeave(HrLeaveCreateVM model)
        {
            if (model == null)
            {
                model = new HrLeaveCreateVM();
                await PopulateEmployeeOptionsForCreateAsync(model);
                return View(model);
            }

            var startDate = ParseJalaliToGregorian(model.StartDate);
            var endDate = ParseJalaliToGregorian(model.EndDate);

            if (!startDate.HasValue)
                ModelState.AddModelError("StartDate", "تاریخ شروع معتبر نیست.");
            if (!endDate.HasValue)
                ModelState.AddModelError("EndDate", "تاریخ پایان معتبر نیست.");

            if (!ModelState.IsValid)
            {
                await PopulateEmployeeOptionsForCreateAsync(model);
                return View(model);
            }

            var now = DateTime.Now;
            var leave = new HrLeave
            {
                EmployeeId = model.EmployeeId,
                LeaveType = model.LeaveType?.Trim() ?? "استحقاقی",
                StartDate = startDate!.Value.Date,
                EndDate = endDate!.Value.Date,
                Description = model.Description?.Trim(),
                Status = model.Status ?? "در انتظار",
                TenantId = _tenantIsolationService.GetCurrent().TenantId ?? string.Empty,
                CreatedAt = now,
                UpdatedAt = now
            };

            _context.HrLeaves.Add(leave);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Leaves));
        }

        // ── Loans ─────────────────────────────────────────────────
        public async Task<IActionResult> Loans()
        {
            ViewData["Title"] = "وام و مساعده";
            ViewData["SectionTitle"] = "وام و مساعده";

            var loans = await _context.HrLoans
                .AsNoTracking()
                .Include(l => l.Employee)
                .OrderByDescending(l => l.CreatedAt)
                .Select(l => new HrLoanItemVM
                {
                    Id = l.Id,
                    EmployeeId = l.EmployeeId,
                    EmployeeName = l.Employee != null ? l.Employee.FullName : "—",
                    PersonnelCode = l.Employee != null ? l.Employee.PersonnelCode : "—",
                    RequestDate = l.RequestDate,
                    Amount = l.Amount,
                    InstallmentCount = l.InstallmentCount,
                    Status = l.Status
                })
                .ToListAsync();

            var vm = new HrLoanIndexVM
            {
                Items = loans,
                TotalCount = loans.Count,
                TotalAmount = loans.Sum(l => l.Amount),
                PendingCount = loans.Count(l => l.Status == "در انتظار")
            };

            return View(vm);
        }

        public async Task<IActionResult> CreateLoan()
        {
            var model = new HrLoanCreateVM();
            await PopulateEmployeeOptionsForLoanAsync(model);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("HR.Create")]
        public async Task<IActionResult> CreateLoan(HrLoanCreateVM model)
        {
            if (model == null)
            {
                model = new HrLoanCreateVM();
                await PopulateEmployeeOptionsForLoanAsync(model);
                return View(model);
            }

            var requestDate = ParseJalaliToGregorian(model.RequestDate);
            if (!requestDate.HasValue)
                ModelState.AddModelError("RequestDate", "تاریخ درخواست معتبر نیست.");

            if (!ModelState.IsValid)
            {
                await PopulateEmployeeOptionsForLoanAsync(model);
                return View(model);
            }

            var now = DateTime.Now;
            var loan = new HrLoan
            {
                EmployeeId = model.EmployeeId,
                RequestDate = requestDate!.Value.Date,
                Amount = model.Amount,
                InstallmentCount = model.InstallmentCount,
                Status = model.Status ?? "در انتظار",
                Description = model.Description?.Trim(),
                TenantId = _tenantIsolationService.GetCurrent().TenantId ?? string.Empty,
                CreatedAt = now,
                UpdatedAt = now
            };

            _context.HrLoans.Add(loan);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Loans));
        }

        // ── Attendance ────────────────────────────────────────────
        public async Task<IActionResult> Attendance()
        {
            ViewData["Title"] = "کارکرد و حضور غیاب";
            ViewData["SectionTitle"] = "کارکرد و حضور غیاب";

            var attendances = await _context.HrAttendances
                .AsNoTracking()
                .Include(a => a.Employee)
                .OrderByDescending(a => a.Year)
                .ThenByDescending(a => a.Month)
                .Select(a => new HrAttendanceItemVM
                {
                    Id = a.Id,
                    EmployeeId = a.EmployeeId,
                    EmployeeName = a.Employee != null ? a.Employee.FullName : "—",
                    PersonnelCode = a.Employee != null ? a.Employee.PersonnelCode : "—",
                    Year = a.Year,
                    Month = a.Month,
                    PresentDays = a.PresentDays,
                    AbsentDays = a.AbsentDays,
                    OvertimeHours = a.OvertimeHours
                })
                .ToListAsync();

            var vm = new HrAttendanceIndexVM
            {
                Items = attendances,
                TotalCount = attendances.Count
            };

            return View(vm);
        }

        public async Task<IActionResult> CreateAttendance()
        {
            var model = new HrAttendanceCreateVM();
            await PopulateEmployeeOptionsForAttendanceAsync(model);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("HR.Create")]
        public async Task<IActionResult> CreateAttendance(HrAttendanceCreateVM model)
        {
            if (model == null)
            {
                model = new HrAttendanceCreateVM();
                await PopulateEmployeeOptionsForAttendanceAsync(model);
                return View(model);
            }

            if (!ModelState.IsValid)
            {
                await PopulateEmployeeOptionsForAttendanceAsync(model);
                return View(model);
            }

            var now = DateTime.Now;
            var attendance = new HrAttendance
            {
                EmployeeId = model.EmployeeId,
                Year = model.Year,
                Month = model.Month,
                PresentDays = model.PresentDays,
                AbsentDays = model.AbsentDays,
                OvertimeHours = model.OvertimeHours,
                Notes = model.Notes?.Trim(),
                TenantId = _tenantIsolationService.GetCurrent().TenantId ?? string.Empty,
                CreatedAt = now,
                UpdatedAt = now
            };

            _context.HrAttendances.Add(attendance);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Attendance));
        }

        // ── Reports ───────────────────────────────────────────────
        public async Task<IActionResult> Reports()
        {
            ViewData["Title"] = "گزارشات جامع منابع انسانی";
            ViewData["SectionTitle"] = "گزارشات جامع منابع انسانی";

            var totalActiveEmployees = await _context.HumanCapitalEmployees
                .AsNoTracking()
                .CountAsync(e => e.CurrentStatus == "فعال");

            var pendingLeavesCount = await _context.HrLeaves
                .AsNoTracking()
                .CountAsync(l => l.Status == "در انتظار");

            var activeLoans = await _context.HrLoans
                .AsNoTracking()
                .Where(l => l.Status == "تایید شده" || l.Status == "پرداخت شده")
                .ToListAsync();

            var totalContracts = await _context.HrContracts.AsNoTracking().CountAsync();
            var activeContracts = await _context.HrContracts.AsNoTracking().CountAsync(c => c.IsActive);

            var totalPayroll = await _context.HumanCapitalEmployees
                .AsNoTracking()
                .Where(e => e.CurrentStatus == "فعال")
                .SumAsync(e => e.CurrentSalary);

            var vm = new HrReportsVM
            {
                TotalActiveEmployees = totalActiveEmployees,
                PendingLeavesCount = pendingLeavesCount,
                ActiveLoansCount = activeLoans.Count,
                TotalLoansAmount = activeLoans.Sum(l => l.Amount),
                TotalContractsCount = totalContracts,
                ActiveContractsCount = activeContracts,
                TotalPayroll = totalPayroll
            };

            return View(vm);
        }

        // ── Helper methods for sub-module employee dropdowns ──────
        private async Task PopulateEmployeeOptionsAsync(HrContractCreateVM model)
        {
            var employees = await _context.HumanCapitalEmployees
                .AsNoTracking()
                .Where(e => e.CurrentStatus == "فعال")
                .OrderBy(e => e.FullName)
                .ToListAsync();

            model.EmployeeOptions = employees.Select(e => new SelectListItem
            {
                Text = $"{e.FullName} ({e.PersonnelCode})",
                Value = e.Id.ToString()
            }).ToList();
        }

        private async Task PopulateEmployeeOptionsForCreateAsync(HrLeaveCreateVM model)
        {
            var employees = await _context.HumanCapitalEmployees
                .AsNoTracking()
                .Where(e => e.CurrentStatus == "فعال")
                .OrderBy(e => e.FullName)
                .ToListAsync();

            model.EmployeeOptions = employees.Select(e => new SelectListItem
            {
                Text = $"{e.FullName} ({e.PersonnelCode})",
                Value = e.Id.ToString()
            }).ToList();
        }

        private async Task PopulateEmployeeOptionsForLoanAsync(HrLoanCreateVM model)
        {
            var employees = await _context.HumanCapitalEmployees
                .AsNoTracking()
                .Where(e => e.CurrentStatus == "فعال")
                .OrderBy(e => e.FullName)
                .ToListAsync();

            model.EmployeeOptions = employees.Select(e => new SelectListItem
            {
                Text = $"{e.FullName} ({e.PersonnelCode})",
                Value = e.Id.ToString()
            }).ToList();
        }

        private async Task PopulateEmployeeOptionsForAttendanceAsync(HrAttendanceCreateVM model)
        {
            var employees = await _context.HumanCapitalEmployees
                .AsNoTracking()
                .Where(e => e.CurrentStatus == "فعال")
                .OrderBy(e => e.FullName)
                .ToListAsync();

            model.EmployeeOptions = employees.Select(e => new SelectListItem
            {
                Text = $"{e.FullName} ({e.PersonnelCode})",
                Value = e.Id.ToString()
            }).ToList();
        }

        [HttpGet]
        public async Task<IActionResult> ExportExcel()
        {
            // Tenant isolation is enforced at the schema level (schema-per-tenant).
            // Do NOT apply department scope filtering — export should include all employees.
            var query = _context.HumanCapitalEmployees
                .AsNoTracking()
                .Include(employee => employee.Department)
                .AsQueryable();

            var employees = await query
                .OrderBy(employee => employee.FullName)
                .Select(employee => new
                {
                    employee.PersonnelCode,
                    employee.FullName,
                    DepartmentName = employee.Department != null ? employee.Department.Name : null,
                    employee.PositionTitle,
                    employee.NationalCode,
                    employee.PhoneNumber,
                    employee.EmploymentType,
                    employee.HireDate,
                    employee.CurrentSalary,
                    employee.CurrentStatus
                })
                .ToListAsync();

            var table = new System.Data.DataTable("Employees");
            table.Columns.Add("کد پرسنلی", typeof(string));
            table.Columns.Add("نام و نام خانوادگی", typeof(string));
            table.Columns.Add("دپارتمان", typeof(string));
            table.Columns.Add("عنوان شغلی", typeof(string));
            table.Columns.Add("کد ملی", typeof(string));
            table.Columns.Add("شماره تماس", typeof(string));
            table.Columns.Add("نوع همکاری", typeof(string));
            table.Columns.Add("تاریخ استخدام", typeof(string));
            table.Columns.Add("حقوق پایه", typeof(decimal));
            table.Columns.Add("وضعیت", typeof(string));

            foreach (var employee in employees)
            {
                table.Rows.Add(
                    employee.PersonnelCode,
                    employee.FullName,
                    employee.DepartmentName ?? string.Empty,
                    employee.PositionTitle,
                    employee.NationalCode,
                    employee.PhoneNumber ?? string.Empty,
                    employee.EmploymentType,
                    employee.HireDate.ToString("yyyy/MM/dd"),
                    employee.CurrentSalary,
                    employee.CurrentStatus);
            }

            var bytes = _exportService.ExportToExcel(table, "پرسنل");
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"human-capital-{DateTime.Now:yyyyMMdd-HHmm}.xlsx");
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
                Text = LocalizeDepartmentName(d.Name),
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
                Text = LocalizeDepartmentName(d.Name),
                Value = d.Id.ToString(),
                Selected = d.Id == selectedDepartmentId
            }).ToList();
            options.Insert(0, new SelectListItem { Value = "", Text = "--- همه دپارتمان‌ها ---" });
            return options;
        }

        private async Task<List<string>> BuildStatusOptionsAsync()
        {
            return DefaultStatuses.ToList();
        }

        private async Task ValidateEmployeeUniquenessAsync(string? personnelCode, string? nationalCode, int? excludeId = null)
        {
            var normalizedPersonnelCode = personnelCode?.Trim() ?? string.Empty;
            var normalizedNationalCode = nationalCode?.Trim() ?? string.Empty;

            var existingEmployee = await _context.HumanCapitalEmployees
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id != (excludeId ?? 0) &&
                    (e.PersonnelCode.Trim().ToLower() == normalizedPersonnelCode.ToLower() ||
                     e.NationalCode.Trim().ToLower() == normalizedNationalCode.ToLower()));

            if (existingEmployee != null)
            {
                if (existingEmployee.PersonnelCode.Trim().Equals(normalizedPersonnelCode, StringComparison.OrdinalIgnoreCase))
                {
                    ModelState.AddModelError("PersonnelCode", "کد پرسنلی تکراری است.");
                }
                if (existingEmployee.NationalCode.Trim().Equals(normalizedNationalCode, StringComparison.OrdinalIgnoreCase))
                {
                    ModelState.AddModelError("NationalCode", "کد ملی تکراری است.");
                }
            }
        }

        private void ValidateInitialStatus(DateTime initialStatusDate, DateTime hireDate)
        {
            if (initialStatusDate.Date < hireDate.Date)
            {
                ModelState.AddModelError("InitialStatusDate", "تاریخ شروع وضعیت اولیه باید بعد از تاریخ استخدام باشد.");
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

        private int? ResolveDepartmentId(int? departmentId)
        {
            // A valid selection made in the (department-scoped) dropdown is always honoured.
            // Otherwise fall back to the user's assigned department (data isolation).
            return departmentId ?? _dataAccessScope.DepartmentId;
        }

        private static string LocalizeDepartmentName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return name ?? string.Empty;
            }

            var normalized = name.Trim();
            return DepartmentPersianNames.TryGetValue(normalized, out var persian)
                ? persian
                : normalized;
        }

        /// <summary>
        /// Converts Persian/Arabic Unicode digits to standard ASCII English digits.
        /// </summary>
        private static string? NormalizePersianDigits(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return input;
            return input.Replace('۰', '0').Replace('۱', '1').Replace('۲', '2')
                        .Replace('۳', '3').Replace('۴', '4').Replace('۵', '5')
                        .Replace('۶', '6').Replace('۷', '7').Replace('۸', '8')
                        .Replace('۹', '9');
        }

        /// <summary>
        /// Parses a Jalali (Persian calendar) date string into a nullable Gregorian <see cref="DateTime"/>.
        /// Accepts Persian/Arabic Unicode digits and '/' or '-' separators.
        /// </summary>
        private static DateTime? ParseJalaliToGregorian(string? jalaliStr)
        {
            var normalized = NormalizePersianDigits(jalaliStr);
            if (string.IsNullOrWhiteSpace(normalized)) return null;

            // Accept ISO Gregorian as-is for compatibility.
            if (DateTime.TryParse(normalized, CultureInfo.InvariantCulture, DateTimeStyles.None, out var isoDate))
            {
                return isoDate.Date;
            }

            var parts = normalized.Split('/', '-', '.');
            if (parts.Length != 3 ||
                !int.TryParse(parts[0], out var year) ||
                !int.TryParse(parts[1], out var month) ||
                !int.TryParse(parts[2], out var day))
            {
                return null;
            }

            if (year < 1200 || year > 1600 || month < 1 || month > 12 || day < 1 || day > 31)
            {
                return null;
            }

            try
            {
                return new PersianCalendar().ToDateTime(year, month, day, 0, 0, 0, 0).Date;
            }
            catch (ArgumentOutOfRangeException)
            {
                return null;
            }
        }

        /// <summary>
        /// Resolves the value of a date form field and updates the corresponding model property.
        /// Removes any model-state binding error for that field so validation can pass cleanly.
        /// </summary>
        private bool TryBindJalaliDate(string formFieldName, Action<string?> assign)
        {
            var raw = Request.Form[formFieldName].ToString();
            if (string.IsNullOrWhiteSpace(raw))
            {
                assign(null);
                ModelState.Remove(formFieldName);
                return true;
            }

            var parsed = ParseJalaliToGregorian(raw);
            if (parsed.HasValue)
            {
                assign(parsed.Value.ToString("yyyy-MM-dd"));
                ModelState.Remove(formFieldName);
                return true;
            }

            return false;
        }

        private void BindJalaliDateFields(HumanCapitalCreateVM model)
        {
            TryBindJalaliDate(nameof(HumanCapitalCreateVM.BirthDate), d => model.BirthDate = d);
            TryBindJalaliDate(nameof(HumanCapitalCreateVM.HireDate), d => model.HireDate = d);
            TryBindJalaliDate(nameof(HumanCapitalCreateVM.ContractEndDate), d => model.ContractEndDate = d);
            TryBindJalaliDate(nameof(HumanCapitalCreateVM.InitialStatusDate), d => model.InitialStatusDate = d);
        }

        private void BindJalaliDateFields(HumanCapitalEditVM model)
        {
            TryBindJalaliDate(nameof(HumanCapitalEditVM.BirthDate), d => model.BirthDate = d);
            TryBindJalaliDate(nameof(HumanCapitalEditVM.HireDate), d => model.HireDate = d);
            TryBindJalaliDate(nameof(HumanCapitalEditVM.ContractEndDate), d => model.ContractEndDate = d);
        }

        private bool IsJsonRequest()
        {
            var accept = HttpContext.Request.Headers["Accept"].ToString();
            if (accept.Contains("application/json", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var requestedWith = HttpContext.Request.Headers["X-Requested-With"].ToString();
            return string.Equals(requestedWith, "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);
        }

        private object BuildValidationErrorResponse()
        {
            var errors = ModelState
                .Where(entry => entry.Value?.Errors.Count > 0)
                .ToDictionary(
                    entry => entry.Key,
                    entry => entry.Value!.Errors.Select(error => error.ErrorMessage).ToArray());

            return new { errors };
        }
    }
}