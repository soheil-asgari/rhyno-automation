using System.ComponentModel.DataAnnotations;

namespace OfficeAutomation.DTOs.Api.v1;

/// <summary>
/// فیلتر جستجوی اسناد بایگانی
/// </summary>
public class DocumentFilterRequest
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
    /// فیلتر دسته‌بندی (Letters/Invoices/Personnel/Contracts/Insurance/Warehouse)
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// جستجوی متنی در عنوان یا نام فایل
    /// </summary>
    public string? Search { get; set; }
}

/// <summary>
/// مدل خلاصه سند بایگانی
/// </summary>
public class DocumentSummaryDto
{
    /// <summary>
    /// شناسه یکتای سند
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// عنوان سند
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// دسته‌بندی
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// سطح دسترسی
    /// </summary>
    public string AccessLevel { get; set; } = string.Empty;

    /// <summary>
    /// نام فایل اصلی
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// نوع محتوا
    /// </summary>
    public string? ContentType { get; set; }

    /// <summary>
    /// حجم فایل (بایت)
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    /// تاریخ ایجاد
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// نام ایجادکننده
    /// </summary>
    public string? CreatorName { get; set; }

    public static DocumentSummaryDto From(Models.DocumentArchiveItem item) => new()
    {
        Id = item.Id,
        Title = item.Title,
        Category = item.Category,
        AccessLevel = item.AccessLevel,
        FileName = item.FileName,
        ContentType = item.ContentType,
        FileSize = item.FileSize,
        CreatedAt = item.CreatedAt,
        CreatorName = item.CreatedByUser?.FullName ?? item.CreatedByUser?.UserName ?? "کاربر"
    };
}

/// <summary>
/// مدل درخواست آپلود سند جدید
/// </summary>
public class DocumentUploadRequest
{
    /// <summary>
    /// فایل مورد نظر برای آپلود (الزامی - حداکثر ۲۰ مگابایت)
    /// </summary>
    [Required(ErrorMessage = "فایل الزامی است.")]
    public IFormFile File { get; set; } = null!;

    /// <summary>
    /// عنوان سند (الزامی)
    /// </summary>
    [Required(ErrorMessage = "عنوان سند الزامی است.")]
    [StringLength(180)]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// دسته‌بندی (پیش‌فرض: Letters)
    /// </summary>
    [Required]
    [StringLength(40)]
    public string Category { get; set; } = "Letters";

    /// <summary>
    /// سطح دسترسی (Public/Module/Restricted)
    /// </summary>
    [Required]
    [StringLength(40)]
    public string AccessLevel { get; set; } = "Module";

    /// <summary>
    /// ماژول مرتبط
    /// </summary>
    [StringLength(120)]
    public string? RelatedModule { get; set; }

    /// <summary>
    /// نوع موجودیت مرتبط
    /// </summary>
    [StringLength(120)]
    public string? RelatedEntityType { get; set; }

    /// <summary>
    /// شناسه موجودیت مرتبط
    /// </summary>
    public int? RelatedEntityId { get; set; }
}

/// <summary>
/// مدل دانلود فایل سند
/// </summary>
public class DocumentDownloadDto
{
    /// <summary>
    /// شناسه سند
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// نام فایل اصلی
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// نوع محتوا
    /// </summary>
    public string? ContentType { get; set; }

    /// <summary>
    /// محتوای فایل (Base64)
    /// </summary>
    public string Base64Content { get; set; } = string.Empty;
}