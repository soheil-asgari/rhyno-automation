using System.ComponentModel.DataAnnotations;

namespace OfficeAutomation.Models;

public class WorkflowDelegation
{
    public int Id { get; set; }

    [Required]
    [StringLength(450)]
    public string FromUserId { get; set; } = string.Empty;

    public User? FromUser { get; set; }

    [Required]
    [StringLength(450)]
    public string ToUserId { get; set; } = string.Empty;

    public User? ToUser { get; set; }

    [StringLength(60)]
    public string? DocumentType { get; set; }

    public DateTimeOffset StartsAt { get; set; }

    public DateTimeOffset EndsAt { get; set; }

    public bool IsActive { get; set; } = true;
}
