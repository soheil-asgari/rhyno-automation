using System.ComponentModel.DataAnnotations;

namespace OfficeAutomation.Models
{
    /// <summary>
    /// فایل‌های پیوست قرارداد
    /// </summary>
    public class ContractAttachment
    {
        public int Id { get; set; }

        [Required]
        public int ContractId { get; set; }

        public Contract? Contract { get; set; }

        [Required]
        [StringLength(260)]
        public string FileName { get; set; } = string.Empty;

        [Required]
        [StringLength(260)]
        public string StoredFileName { get; set; } = string.Empty;

        [Required]
        [StringLength(400)]
        public string RelativePath { get; set; } = string.Empty;

        [StringLength(120)]
        public string? ContentType { get; set; }

        public long FileSize { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Required]
        [MaxLength(450)]
        public string UploadedByUserId { get; set; } = string.Empty;
    }
}