using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace OfficeAutomation.Models
{
    // ── Contracts VMs ─────────────────────────────────────────────
    public class HrContractIndexVM
    {
        public List<HrContractItemVM> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int ActiveCount { get; set; }
    }

    public class HrContractItemVM
    {
        public int Id { get; set; }
        public int EmployeeId { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public string PersonnelCode { get; set; } = string.Empty;
        public string ContractType { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public decimal BaseSalary { get; set; }
        public bool IsActive { get; set; }
    }

    public class HrContractCreateVM
    {
        [Required(ErrorMessage = "انتخاب کارمند الزامی است.")]
        [Display(Name = "کارمند")]
        public int EmployeeId { get; set; }

        [Required(ErrorMessage = "نوع قرارداد الزامی است.")]
        [StringLength(60)]
        [Display(Name = "نوع قرارداد")]
        public string ContractType { get; set; } = "تمام وقت";

        [Required(ErrorMessage = "تاریخ شروع الزامی است.")]
        [DataType(DataType.Date)]
        [Display(Name = "تاریخ شروع")]
        public string? StartDate { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "تاریخ پایان")]
        public string? EndDate { get; set; }

        [Range(0, 99999999999, ErrorMessage = "مبلغ حقوق معتبر نیست.")]
        [Display(Name = "حقوق پایه")]
        public decimal BaseSalary { get; set; }

        [Display(Name = "فعال")]
        public bool IsActive { get; set; } = true;

        [StringLength(500)]
        [Display(Name = "توضیحات")]
        public string? Notes { get; set; }

        public List<SelectListItem> EmployeeOptions { get; set; } = new();
        public List<string> ContractTypeOptions { get; set; } = new()
        {
            "تمام وقت", "پاره وقت", "پروژه‌ای", "کارآموزی", "نیروی دورکار"
        };
    }

    // ── Leaves VMs ────────────────────────────────────────────────
    public class HrLeaveIndexVM
    {
        public List<HrLeaveItemVM> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int PendingCount { get; set; }
        public int ApprovedCount { get; set; }
    }

    public class HrLeaveItemVM
    {
        public int Id { get; set; }
        public int EmployeeId { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public string PersonnelCode { get; set; } = string.Empty;
        public string LeaveType { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string? Description { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    public class HrLeaveCreateVM
    {
        [Required(ErrorMessage = "انتخاب کارمند الزامی است.")]
        [Display(Name = "کارمند")]
        public int EmployeeId { get; set; }

        [Required(ErrorMessage = "نوع مرخصی الزامی است.")]
        [StringLength(30)]
        [Display(Name = "نوع مرخصی")]
        public string LeaveType { get; set; } = "استحقاقی";

        [Required(ErrorMessage = "تاریخ شروع الزامی است.")]
        [DataType(DataType.Date)]
        [Display(Name = "تاریخ شروع")]
        public string? StartDate { get; set; }

        [Required(ErrorMessage = "تاریخ پایان الزامی است.")]
        [DataType(DataType.Date)]
        [Display(Name = "تاریخ پایان")]
        public string? EndDate { get; set; }

        [StringLength(500)]
        [Display(Name = "توضیحات")]
        public string? Description { get; set; }

        [StringLength(30)]
        [Display(Name = "وضعیت")]
        public string Status { get; set; } = "در انتظار";

        public List<SelectListItem> EmployeeOptions { get; set; } = new();
        public List<string> LeaveTypeOptions { get; set; } = new()
        {
            "استحقاقی", "استعلاجی", "مأموریت", "بدون حقوق", "تشویقی"
        };
        public List<string> StatusOptions { get; set; } = new()
        {
            "در انتظار", "تایید شده", "رد شده"
        };
    }

    // ── Loans VMs ─────────────────────────────────────────────────
    public class HrLoanIndexVM
    {
        public List<HrLoanItemVM> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public decimal TotalAmount { get; set; }
        public int PendingCount { get; set; }
    }

    public class HrLoanItemVM
    {
        public int Id { get; set; }
        public int EmployeeId { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public string PersonnelCode { get; set; } = string.Empty;
        public DateTime RequestDate { get; set; }
        public decimal Amount { get; set; }
        public int InstallmentCount { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    public class HrLoanCreateVM
    {
        [Required(ErrorMessage = "انتخاب کارمند الزامی است.")]
        [Display(Name = "کارمند")]
        public int EmployeeId { get; set; }

        [Required(ErrorMessage = "تاریخ درخواست الزامی است.")]
        [DataType(DataType.Date)]
        [Display(Name = "تاریخ درخواست")]
        public string? RequestDate { get; set; }

        [Required(ErrorMessage = "مبلغ وام الزامی است.")]
        [Range(1, 99999999999, ErrorMessage = "مبلغ وام معتبر نیست.")]
        [Display(Name = "مبلغ وام")]
        public decimal Amount { get; set; }

        [Required(ErrorMessage = "تعداد اقساط الزامی است.")]
        [Range(1, 120, ErrorMessage = "تعداد اقساط باید بین ۱ تا ۱۲۰ باشد.")]
        [Display(Name = "تعداد اقساط")]
        public int InstallmentCount { get; set; } = 12;

        [StringLength(30)]
        [Display(Name = "وضعیت")]
        public string Status { get; set; } = "در انتظار";

        [StringLength(500)]
        [Display(Name = "توضیحات")]
        public string? Description { get; set; }

        public List<SelectListItem> EmployeeOptions { get; set; } = new();
        public List<string> StatusOptions { get; set; } = new()
        {
            "در انتظار", "تایید شده", "رد شده", "پرداخت شده"
        };
    }

    // ── Attendance VMs ────────────────────────────────────────────
    public class HrAttendanceIndexVM
    {
        public List<HrAttendanceItemVM> Items { get; set; } = new();
        public int TotalCount { get; set; }
    }

    public class HrAttendanceItemVM
    {
        public int Id { get; set; }
        public int EmployeeId { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public string PersonnelCode { get; set; } = string.Empty;
        public int Year { get; set; }
        public int Month { get; set; }
        public int PresentDays { get; set; }
        public int AbsentDays { get; set; }
        public decimal OvertimeHours { get; set; }
    }

    public class HrAttendanceCreateVM
    {
        [Required(ErrorMessage = "انتخاب کارمند الزامی است.")]
        [Display(Name = "کارمند")]
        public int EmployeeId { get; set; }

        [Required(ErrorMessage = "سال الزامی است.")]
        [Range(1390, 1420, ErrorMessage = "سال معتبر نیست.")]
        [Display(Name = "سال")]
        public int Year { get; set; } = 1405;

        [Required(ErrorMessage = "ماه الزامی است.")]
        [Range(1, 12, ErrorMessage = "ماه باید بین ۱ تا ۱۲ باشد.")]
        [Display(Name = "ماه")]
        public int Month { get; set; } = 1;

        [Range(0, 31)]
        [Display(Name = "روزهای حضور")]
        public int PresentDays { get; set; }

        [Range(0, 31)]
        [Display(Name = "روزهای غیبت")]
        public int AbsentDays { get; set; }

        [Range(0, 200)]
        [Display(Name = "ساعات اضافه‌کار")]
        public decimal OvertimeHours { get; set; }

        [StringLength(500)]
        [Display(Name = "توضیحات")]
        public string? Notes { get; set; }

        public List<SelectListItem> EmployeeOptions { get; set; } = new();
        public List<string> MonthOptions { get; set; } = new()
        {
            "فروردین", "اردیبهشت", "خرداد", "تیر", "مرداد", "شهریور",
            "مهر", "آبان", "آذر", "دی", "بهمن", "اسفند"
        };
    }

    // ── Reports VM ────────────────────────────────────────────────
    public class HrReportsVM
    {
        public int TotalActiveEmployees { get; set; }
        public int PendingLeavesCount { get; set; }
        public int ActiveLoansCount { get; set; }
        public decimal TotalLoansAmount { get; set; }
        public int TotalContractsCount { get; set; }
        public int ActiveContractsCount { get; set; }
        public decimal TotalPayroll { get; set; }
    }
}