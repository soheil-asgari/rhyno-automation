using OfficeAutomation.Models;

namespace OfficeAutomation.Services.Contracts;

/// <summary>
/// سرویس مدیریت الزامات مدارک برای هر نوع قرارداد
/// </summary>
public interface IDocumentRequirementService
{
    /// <summary>دریافت لیست مدارک مورد نیاز برای یک نوع قرارداد</summary>
    Task<List<DocumentRequirement>> GetRequirementsForTypeAsync(string contractType, CancellationToken ct = default);

    /// <summary>دریافت checklist مدارک برای یک قرارداد مشخص (با وضعیت آپلود)</summary>
    Task<DocumentRequirementChecklistVM> GetChecklistAsync(int contractId, CancellationToken ct = default);

    /// <summary>
    /// بررسی اینکه آیا تمام مدارک الزامی برای یک قرارداد آپلود شده است یا خیر.
    /// اگر مدارکی missing باشد، نام آنها در خروجی برمی‌گردد.
    /// </summary>
    Task<(bool AllUploaded, List<string> MissingNames)> ValidateRequiredDocumentsAsync(int contractId, CancellationToken ct = default);

    /// <summary>درج پیش‌فرض مدارک برای یک نوع قرارداد (فقط در صورت عدم وجود)</summary>
    Task<int> SeedDefaultRequirementsAsync(string contractType, CancellationToken ct = default);

    /// <summary>درج پیش‌فرض مدارک برای همه نوع قراردادهای تعریف شده</summary>
    Task<int> SeedAllDefaultsAsync(CancellationToken ct = default);

    /// <summary>ذخیره / به‌روزرسانی یک نیازمندی مدرک</summary>
    Task<DocumentRequirement> SaveRequirementAsync(DocumentRequirement requirement, CancellationToken ct = default);

    /// <summary>حذف یک نیازمندی مدرک</summary>
    Task<bool> DeleteRequirementAsync(int id, CancellationToken ct = default);
}