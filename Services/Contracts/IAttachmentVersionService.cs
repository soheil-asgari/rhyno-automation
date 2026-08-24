using OfficeAutomation.Models;

namespace OfficeAutomation.Services.Contracts;

/// <summary>
/// سرویس نسخه‌بندی فایل‌های پیوست قرارداد
/// </summary>
public interface IAttachmentVersionService
{
    /// <summary>
    /// آپلود نسخه جدید از یک فایل پیوست
    /// </summary>
    Task<ContractAttachmentVersion> UploadNewVersionAsync(
        int attachmentId,
        string fileName,
        string contentType,
        long fileSize,
        Stream fileStream,
        string currentUserId,
        string? changeDescription = null,
        CancellationToken ct = default);

    /// <summary>
    /// دریافت لیست نسخه‌های یک پیوست
    /// </summary>
    Task<List<AttachmentVersionVM>> GetVersionsAsync(int attachmentId, CancellationToken ct = default);

    /// <summary>
    /// دریافت مسیر فایل یک نسخه خاص برای دانلود
    /// </summary>
    Task<string?> GetVersionFilePathAsync(int versionId, string userId, CancellationToken ct = default);

    /// <summary>
    /// بازگردانی نسخه قدیمی (کپی محتوا به عنوان نسخه جدید)
    /// </summary>
    Task<ContractAttachmentVersion> RestoreVersionAsync(
        int versionId,
        string currentUserId,
        CancellationToken ct = default);
}