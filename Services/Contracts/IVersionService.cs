using OfficeAutomation.Models;

namespace OfficeAutomation.Services.Contracts;

/// <summary>
/// سرویس نسخه‌بندی و اسنپشات قراردادها
/// </summary>
public interface IVersionService
{
    /// <summary>
    /// ایجاد یک نسخه جدید از قرارداد با اسنپشات وضعیت فعلی فیلدها.
    /// اگر <paramref name="skipIfUnchanged"/> true باشد و هیچ تغییری نسبت به آخرین نسخه
    /// پیدا نشود، نسخه جدیدی ثبت نمی‌کند و null برمی‌گرداند.
    /// </summary>
    /// <param name="contract">قرارداد با وضعیت فعلی فیلدها</param>
    /// <param name="currentUserId">کاربر تغییردهنده</param>
    /// <param name="changeType">نوع رویداد</param>
    /// <param name="changeDescription">توضیح تغییرات</param>
    /// <param name="isMajor">اگر true باشد نسخه major افزایش می‌یابد، وگرنه minor</param>
    /// <param name="skipIfUnchanged">اگر true باشد در صورت عدم تغییر نسبت به آخرین نسخه، اسنپشات جدید نمی‌سازد</param>
    /// <returns>نسخه ایجاد شده یا null در صورت نادیده گرفتن</returns>
    Task<ContractVersion?> CreateVersionSnapshotAsync(
        Contract contract, string currentUserId,
        string changeType = "Edited",
        string? changeDescription = null,
        bool isMajor = false,
        bool skipIfUnchanged = false,
        CancellationToken ct = default);

    /// <summary>
    /// دریافت آخرین نسخه ثبت شده از یک قرارداد
    /// </summary>
    Task<ContractVersion?> GetLatestVersionAsync(int contractId, CancellationToken ct = default);

    /// <summary>
    /// دریافت لیست تمام نسخه‌های یک قرارداد
    /// </summary>
    Task<List<ContractVersion>> GetVersionsForContractAsync(int contractId, CancellationToken ct = default);

    /// <summary>
    /// دریافت یک نسخه خاص
    /// </summary>
    Task<ContractVersion?> GetVersionByIdAsync(int versionId, CancellationToken ct = default);

    /// <summary>
    /// مقایسه دو نسخه و برگرداندن فیلدهای تغییر کرده
    /// </summary>
    Task<List<VersionDiffEntry>> CompareVersionsAsync(int versionIdA, int versionIdB, CancellationToken ct = default);

    /// <summary>
    /// دریافت نسخه یک قرارداد در زمان خاص (تاریخ معین)
    /// </summary>
    Task<ContractVersion?> GetVersionAsOfAsync(int contractId, DateTime asOfDate, CancellationToken ct = default);
}