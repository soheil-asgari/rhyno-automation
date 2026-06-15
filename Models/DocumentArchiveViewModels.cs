using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace OfficeAutomation.Models
{
    public sealed class DocumentArchiveUploadVM
    {
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
        public IFormFile? File { get; set; }
    }

    public sealed class DocumentArchiveItemVM
    {
        public int Id { get; init; }
        public string Title { get; init; } = string.Empty;
        public string Category { get; init; } = string.Empty;
        public string AccessLevel { get; init; } = string.Empty;
        public string FileName { get; init; } = string.Empty;
        public string RelativePath { get; init; } = string.Empty;
        public string? ContentType { get; init; }
        public long FileSize { get; init; }
        public bool IsPreviewable { get; init; }
        public DateTime CreatedAt { get; init; }
        public string CreatorName { get; init; } = string.Empty;
    }

    public sealed class DocumentArchiveIndexVM
    {
        public string? SearchTerm { get; set; }
        public string? Category { get; set; }
        public string? AccessLevel { get; set; }
        public IReadOnlyList<DocumentArchiveItemVM> Items { get; init; } = [];
        public DocumentArchiveUploadVM Upload { get; init; } = new();
        public bool CanUpload { get; init; }
    }
}
