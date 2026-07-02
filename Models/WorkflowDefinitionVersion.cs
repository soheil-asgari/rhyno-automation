using System.ComponentModel.DataAnnotations;

namespace OfficeAutomation.Models;

public class WorkflowDefinitionVersion
{
    public int Id { get; set; }

    [Required]
    [StringLength(60)]
    public string DocumentType { get; set; } = string.Empty;

    public int Version { get; set; }

    public bool IsActive { get; set; } = true;

    [Required]
    [StringLength(20)]
    public string DeploymentMode { get; set; } = WorkflowDeploymentMode.Stable;

    public int TrafficPercentage { get; set; } = 100;

    [StringLength(20)]
    public string? DeploymentRing { get; set; }

    public int? RollbackOfVersionId { get; set; }

    public DateTimeOffset EffectiveFrom { get; set; }

    public DateTimeOffset? EffectiveTo { get; set; }

    public List<WorkflowStepDefinition> StepDefinitions { get; set; } = new();

    public List<WorkflowInstance> WorkflowInstances { get; set; } = new();
}

public static class WorkflowDeploymentMode
{
    public const string Stable = "Stable";
    public const string Canary = "Canary";
    public const string BlueGreen = "BlueGreen";
}
