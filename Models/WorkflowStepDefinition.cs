using System.ComponentModel.DataAnnotations;

namespace OfficeAutomation.Models;

public class WorkflowStepDefinition
{
    public int Id { get; set; }

    public int DefinitionVersionId { get; set; }

    public WorkflowDefinitionVersion? DefinitionVersion { get; set; }

    [Required]
    [StringLength(80)]
    public string StepKey { get; set; } = string.Empty;

    public int StepOrder { get; set; }

    [Required]
    [StringLength(30)]
    public string AssignmentMode { get; set; } = WorkflowAssignmentMode.User;

    public int SlaHours { get; set; } = 24;

    public int EscalationHours { get; set; } = 48;

    public List<WorkflowRule> Rules { get; set; } = new();
}
