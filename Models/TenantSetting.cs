using System.ComponentModel.DataAnnotations;

namespace OfficeAutomation.Models;

public sealed class TenantSetting
{
    public int Id { get; set; }

    [Required]
    [StringLength(64)]
    public string TenantId { get; set; } = string.Empty;

    [Required]
    [StringLength(128)]
    public string Key { get; set; } = string.Empty;

    [Required]
    [StringLength(2000)]
    public string Value { get; set; } = string.Empty;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
