using System.ComponentModel.DataAnnotations;

namespace OfficeAutomation.Models;

/// <summary>
/// نگهداری نسخه‌های قبلی اسناد آرشیو شده
/// </summary>
public class DocumentVersion
{
    public int Id { get; set; }

    public int DocumentArchiveItemId { get; set; }
    public DocumentArchiveItem? DocumentArchiveItem { get; set; }

    /// <summary>شماره نسخه مثل 1.0, 1.1, 2.0</summary>
    [Required]
    [StringLength(20)]
    public string VersionNumber { get; set; } = "1.0";

    /// <summary>مسیر فایل نسخه قبلی</summary>
    [Required]
    [StringLength(260)]
    public string FilePath { get; set; } = string.Empty;

    /// <summary>نام ذخیره‌شده فایل نسخه قبلی</summary>
    [Required]
    [StringLength(260)]
    public string StoredFileName { get; set; } = string.Empty;

    /// <summary>مسیر نسبی فایل</summary>
    [Required]
    [StringLength(400)]
    public string RelativePath { get; set; } = string.Empty;

    [StringLength(120)]
    public string? ContentType { get; set; }

    public long FileSize { get; set; }

    /// <summary>خلاصه تغییرات این نسخه</summary>
    [StringLength(1000)]
    public string? ChangeSummary { get; set; }

    [Required]
    [StringLength(450)]
    public string CreatedByUserId { get; set; } = string.Empty;
    public User? CreatedByUser { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
}