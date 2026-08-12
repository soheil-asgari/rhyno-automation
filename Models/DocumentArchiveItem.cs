using System.ComponentModel.DataAnnotations;

namespace OfficeAutomation.Models
{
    public static class DocumentArchiveModules
    {
        public const string Letters = "Letters";
        public const string Invoices = "Invoices";
        public const string Personnel = "Personnel";
        public const string Contracts = "Contracts";
        public const string Insurance = "Insurance";
        public const string Warehouse = "Warehouse";

        public static readonly string[] All =
        [
            Letters,
            Invoices,
            Personnel,
            Contracts,
            Insurance,
            Warehouse
        ];
    }

    public static class DocumentArchiveVisibilityLevels
    {
        public const string Public = "Public";
        public const string Module = "Module";
        public const string Restricted = "Restricted";

        public static readonly string[] All =
        [
            Public,
            Module,
            Restricted
        ];
    }

    public class DocumentArchiveItem
    {
        public int Id { get; set; }

        [Required]
        [StringLength(180)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [StringLength(40)]
        public string Category { get; set; } = DocumentArchiveModules.Letters;

        [Required]
        [StringLength(40)]
        public string AccessLevel { get; set; } = DocumentArchiveVisibilityLevels.Module;

        [StringLength(120)]
        public string? RelatedModule { get; set; }

        [StringLength(120)]
        public string? RelatedEntityType { get; set; }

        public int? RelatedEntityId { get; set; }

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

        public bool IsPreviewable { get; set; }

        /// <summary>شماره نسخه فعلی سند (مثل 1.0، 2.0)</summary>
        [StringLength(20)]
        public string CurrentVersion { get; set; } = "1.0";

        /// <summary>تاریخ انقضای سند برای سیاست نگهداری</summary>
        public DateTime? ExpirationDate { get; set; }

        /// <summary>وضعیت آرشیو شدن سند</summary>
        public bool IsArchived { get; set; }

        /// <summary>مشاوره نگهداری قانونی (پرونده قضایی)</summary>
        public bool IsUnderLegalHold { get; set; }

        /// <summary>دلیل نگهداری قانونی</summary>
        [StringLength(1000)]
        public string? HoldReason { get; set; }

        [Required]
        public string CreatedByUserId { get; set; } = string.Empty;

        public User? CreatedByUser { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}