using System.ComponentModel.DataAnnotations;

namespace OfficeAutomation.Models;

public class WorkflowRule
{
    public int Id { get; set; }

    public int StepDefinitionId { get; set; }

    public WorkflowStepDefinition? StepDefinition { get; set; }

    [Required]
    [StringLength(120)]
    public string FieldName { get; set; } = string.Empty;

    [Required]
    [StringLength(40)]
    public string Operator { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Value { get; set; }

    [StringLength(80)]
    public string? NextStepKey { get; set; }

    [StringLength(450)]
    public string? AssigneeRoleId { get; set; }

    public ApplicationRole? AssigneeRole { get; set; }

    [StringLength(450)]
    public string? AssigneeUserId { get; set; }

    public User? AssigneeUser { get; set; }

    public int? AssigneeDepartmentId { get; set; }

    public Department? AssigneeDepartment { get; set; }
}
