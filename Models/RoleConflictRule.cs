using System.ComponentModel.DataAnnotations;

namespace OfficeAutomation.Models;

public sealed class RoleConflictRule
{
    public int Id { get; set; }

    [Required]
    [StringLength(128)]
    public string RoleA { get; set; } = string.Empty;

    [Required]
    [StringLength(128)]
    public string RoleB { get; set; } = string.Empty;

    [Required]
    [StringLength(256)]
    public string Reason { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}
