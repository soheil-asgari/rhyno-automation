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

        /// <summary>نسخه فعلی این پیوست (برای ردیابی آخرین نسخه)</summary>
        public int? CurrentVersionId { get; set; }

        public ContractAttachmentVersion? CurrentVersion { get; set; }

        /// <summary>شماره نسخه فعلی (از 1 شروع می‌شود)</summary>
        public int VersionNumber { get; set; } = 1;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Required]
        [MaxLength(450)]
        public string UploadedByUserId { get; set; } = string.Empty;

        public User? UploadedByUser { get; set; }

        public List<ContractAttachmentVersion> Versions { get; set; } = new();
    }

    /// <summary>
    /// نسخه‌های یک فایل پیوست
    /// </summary>
    public class ContractAttachmentVersion
    {
        public int Id { get; set; }

        [Required]
        public int AttachmentId { get; set; }

        public ContractAttachment? Attachment { get; set; }

        /// <summary>شماره نسخه (1, 2, 3, ...)</summary>
        public int VersionNumber { get; set; } = 1;

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

        /// <summary>توضیح تغییرات این نسخه</summary>
        [StringLength(1000)]
        public string? ChangeDescription { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Required]
        [MaxLength(450)]
        public string UploadedByUserId { get; set; } = string.Empty;

        public User? UploadedByUser { get; set; }
    }

    public class AttachmentVersionVM
    {
        public int Id { get; set; }
        public int AttachmentId { get; set; }
        public int VersionNumber { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string? ContentType { get; set; }
        public long FileSize { get; set; }
        public string FileSizeText { get; set; } = string.Empty;
        public string? ChangeDescription { get; set; }
        public string UploadedByUserName { get; set; } = string.Empty;
        public string CreatedAtText { get; set; } = string.Empty;
        public bool IsCurrent { get; set; }
    }

    public class AttachmentVersionUploadVM
    {
        [Required]
        public int AttachmentId { get; set; }

        [StringLength(1000)]
        public string? ChangeDescription { get; set; }
    }
}
