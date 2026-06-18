using System.ComponentModel.DataAnnotations;

namespace OfficeAutomation.Models;

public class LetterAttachment
{
    public int Id { get; set; }

    public int LetterId { get; set; }

    public Letter? Letter { get; set; }

    [Required]
    [StringLength(260)]
    [Display(Name = "نام فایل")]
    public string FileName { get; set; } = string.Empty;

    [Required]
    [StringLength(400)]
    [Display(Name = "مسیر ذخیره‌سازی")]
    public string FilePath { get; set; } = string.Empty;

    [StringLength(120)]
    [Display(Name = "نوع محتوا")]
    public string ContentType { get; set; } = string.Empty;

    [Display(Name = "حجم فایل (بایت)")]
    public long FileSize { get; set; }

    public DateTimeOffset UploadedAt { get; set; } = DateTimeOffset.Now;
}
