using System.ComponentModel.DataAnnotations;

namespace OfficeAutomation.DTOs.Api.v1;

/// <summary>
/// فیلتر جستجوی پرسنل
/// </summary>
public class EmployeeFilterRequest
{
    /// <summary>
    /// شماره صفحه (پیش‌فرض: ۱)
    /// </summary>
    public int Page { get; set; } = 1;

    /// <summary>
    /// تعداد آیتم در هر صفحه (پیش‌فرض: ۲۰، حداکثر: ۱۰۰)
    /// </summary>
    [Range(1, 100)]
    public int PageSize { get; set; } = 20;

    /// <summary>
    /// جستجوی متنی در نام، نام‌خانوادگی یا کد پرسنلی
    /// </summary>
    public string? Search { get; set; }

    /// <summary>
    /// فیلتر بر اساس وضعیت (فعال/تعدیل شده/ترک کار/پایان خدمت)
    /// </summary>
    public string? Status { get; set; }

    /// <summary>
    /// فیلتر بر اساس نوع استخدام
    /// </summary>
    public string? EmploymentType { get; set; }

    /// <summary>
    /// فیلتر بر اساس شناسه دپارتمان
    /// </summary>
    public int? DepartmentId { get; set; }
}

/// <summary>
/// مدل خلاصه پرسنل
/// </summary>
public class EmployeeSummaryDto
{
    /// <summary>
    /// شناسه پرسنل
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// کد پرسنلی
    /// </summary>
    public string PersonnelCode { get; set; } = string.Empty;

    /// <summary>
    /// نام کامل
    /// </summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>
    /// کد ملی
    /// </summary>
    public string NationalCode { get; set; } = string.Empty;

    /// <summary>
    /// عنوان شغلی
    /// </summary>
    public string PositionTitle { get; set; } = string.Empty;

    /// <summary>
    /// نوع استخدام
    /// </summary>
    public string EmploymentType { get; set; } = string.Empty;

    /// <summary>
    /// وضعیت فعلی
    /// </summary>
    public string CurrentStatus { get; set; } = string.Empty;

    /// <summary>
    /// نام دپارتمان
    /// </summary>
    public string? DepartmentName { get; set; }

    /// <summary>
    /// تاریخ استخدام
    /// </summary>
    public DateTime HireDate { get; set; }

    /// <summary>
    /// شماره تماس
    /// </summary>
    public string? PhoneNumber { get; set; }

    public static EmployeeSummaryDto From(Models.HumanCapitalEmployee employee) => new()
    {
        Id = employee.Id,
        PersonnelCode = employee.PersonnelCode,
        FullName = employee.FullName,
        NationalCode = employee.NationalCode,
        PositionTitle = employee.PositionTitle,
        EmploymentType = employee.EmploymentType,
        CurrentStatus = employee.CurrentStatus,
        DepartmentName = employee.Department?.Name,
        HireDate = employee.HireDate,
        PhoneNumber = employee.PhoneNumber
    };
}

/// <summary>
/// مدل خلاصه درخواست مرخصی
/// </summary>
public class LeaveSummaryDto
{
    /// <summary>
    /// شناسه درخواست مرخصی
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// شناسه کاربر درخواست‌دهنده
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// تاریخ شروع
    /// </summary>
    public DateTime StartDate { get; set; }

    /// <summary>
    /// تاریخ پایان
    /// </summary>
    public DateTime EndDate { get; set; }

    /// <summary>
    /// تعداد روز
    /// </summary>
    public int Days => (EndDate - StartDate).Days + 1;

    /// <summary>
    /// دلیل درخواست
    /// </summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// وضعیت (در انتظار/تایید شده/رد شده)
    /// </summary>
    public string Status { get; set; } = string.Empty;

    public static LeaveSummaryDto From(Models.Leave leave) => new()
    {
        Id = leave.Id,
        UserId = leave.UserId,
        StartDate = leave.StartDate,
        EndDate = leave.EndDate,
        Reason = leave.Reason,
        Status = leave.Status
    };
}

/// <summary>
/// مدل ثبت درخواست مرخصی جدید
/// </summary>
public class CreateLeaveRequest
{
    /// <summary>
    /// تاریخ شروع مرخصی (الزامی)
    /// </summary>
    [Required(ErrorMessage = "تاریخ شروع الزامی است.")]
    public DateTime StartDate { get; set; }

    /// <summary>
    /// تاریخ پایان مرخصی (الزامی)
    /// </summary>
    [Required(ErrorMessage = "تاریخ پایان الزامی است.")]
    public DateTime EndDate { get; set; }

    /// <summary>
    /// دلیل درخواست مرخصی (الزامی)
    /// </summary>
    [Required(ErrorMessage = "دلیل درخواست الزامی است.")]
    [StringLength(500, ErrorMessage = "حداکثر طول دلیل ۵۰۰ کاراکتر است.")]
    public string Reason { get; set; } = string.Empty;
}