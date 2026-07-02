using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OfficeAutomation.Models;
using OfficeAutomation.Modules.Workflow.Infrastructure.Persistence;
using OfficeAutomation.Services.Decisioning;

namespace OfficeAutomation.Services;

public interface IWorkflowDefinitionSelector
{
    WorkflowDefinitionVersion? SelectVersion(
        string documentType,
        IReadOnlyList<WorkflowDefinitionVersion> candidates,
        int? documentId,
        string? startedByUserId);
}

public sealed class WorkflowDefinitionSelector : IWorkflowDefinitionSelector
{
    public WorkflowDefinitionVersion? SelectVersion(
        string documentType,
        IReadOnlyList<WorkflowDefinitionVersion> candidates,
        int? documentId,
        string? startedByUserId)
    {
        var eligible = candidates
            .Where(item => item.IsActive)
            .OrderByDescending(item => item.Version)
            .ToList();
        if (eligible.Count == 0)
        {
            return null;
        }

        var canary = eligible
            .Where(item => string.Equals(item.DeploymentMode, WorkflowDeploymentMode.Canary, StringComparison.OrdinalIgnoreCase)
                        && item.TrafficPercentage > 0)
            .OrderByDescending(item => item.Version)
            .FirstOrDefault();

        if (canary != null && IsSelectedForTraffic(documentType, canary, documentId, startedByUserId))
        {
            return canary;
        }

        var blueGreen = eligible
            .Where(item => string.Equals(item.DeploymentMode, WorkflowDeploymentMode.BlueGreen, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(item => item.Version)
            .FirstOrDefault();
        if (blueGreen != null)
        {
            return blueGreen;
        }

        return eligible
            .Where(item => string.Equals(item.DeploymentMode, WorkflowDeploymentMode.Stable, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(item => item.Version)
            .FirstOrDefault() ?? eligible.First();
    }

    private static bool IsSelectedForTraffic(string documentType, WorkflowDefinitionVersion version, int? documentId, string? startedByUserId)
    {
        var seed = $"{documentType}:{documentId?.ToString() ?? "none"}:{startedByUserId ?? "anon"}:{version.Id}:{version.Version}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(seed));
        var bucket = bytes[0] % 100;
        return bucket < version.TrafficPercentage;
    }
}

public sealed class WorkflowGovernanceService
{
    private readonly IWorkflowDbContext _context;
    private readonly IDecisionEngine _decisionEngine;
    private readonly IWorkflowDefinitionSelector _selector;
    private readonly ILogger<WorkflowGovernanceService> _logger;

    public WorkflowGovernanceService(
        IWorkflowDbContext context,
        IDecisionEngine decisionEngine,
        IWorkflowDefinitionSelector selector,
        ILogger<WorkflowGovernanceService>? logger = null)
    {
        _context = context;
        _decisionEngine = decisionEngine;
        _selector = selector;
        _logger = logger ?? NullLogger<WorkflowGovernanceService>.Instance;
    }

    public async Task<WorkflowSimulationReport?> SimulateAsync(
        string documentType,
        int definitionVersionId,
        IEnumerable<WorkflowSimulationScenario> scenarios,
        CancellationToken cancellationToken = default)
    {
        var definition = await _context.WorkflowDefinitionVersions
            .AsNoTracking()
            .Include(item => item.StepDefinitions.OrderBy(step => step.StepOrder))
                .ThenInclude(item => item.Rules)
            .FirstOrDefaultAsync(item => item.Id == definitionVersionId && item.DocumentType == documentType, cancellationToken);
        if (definition == null)
        {
            return null;
        }

        var paths = new List<WorkflowSimulationPath>();
        foreach (var scenario in scenarios)
        {
            var steps = SimulateScenario(definition, scenario);
            paths.Add(new WorkflowSimulationPath
            {
                ScenarioId = scenario.ScenarioId,
                TotalSlaHours = steps.Sum(item => item.SlaHours),
                TotalEscalationHours = steps.Sum(item => item.EscalationHours),
                HasLoop = steps.GroupBy(item => item.StepKey).Any(group => group.Count() > 1),
                Steps = steps
            });
        }

        return new WorkflowSimulationReport
        {
            DocumentType = documentType,
            DefinitionVersionId = definition.Id,
            DefinitionVersion = definition.Version,
            GeneratedAt = DateTimeOffset.UtcNow,
            Paths = paths
        };
    }

    public async Task<WorkflowDeploymentResult?> DeployVersionAsync(
        string documentType,
        int definitionVersionId,
        string deploymentMode,
        int trafficPercentage = 100,
        string? deploymentRing = null,
        CancellationToken cancellationToken = default)
    {
        var target = await _context.WorkflowDefinitionVersions
            .FirstOrDefaultAsync(item => item.Id == definitionVersionId && item.DocumentType == documentType, cancellationToken);
        if (target == null)
        {
            return null;
        }

        var versions = await _context.WorkflowDefinitionVersions
            .Where(item => item.DocumentType == documentType)
            .OrderByDescending(item => item.Version)
            .ToListAsync(cancellationToken);

        var previousStable = versions
            .FirstOrDefault(item => item.IsActive &&
                                    string.Equals(item.DeploymentMode, WorkflowDeploymentMode.Stable, StringComparison.OrdinalIgnoreCase) &&
                                    item.Id != target.Id);

        if (string.Equals(deploymentMode, WorkflowDeploymentMode.Stable, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(deploymentMode, WorkflowDeploymentMode.BlueGreen, StringComparison.OrdinalIgnoreCase))
        {
            foreach (var version in versions.Where(item => item.Id != target.Id))
            {
                if (string.Equals(version.DeploymentMode, WorkflowDeploymentMode.BlueGreen, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(version.DeploymentMode, WorkflowDeploymentMode.Stable, StringComparison.OrdinalIgnoreCase))
                {
                    version.IsActive = false;
                }
            }
        }

        target.IsActive = true;
        target.DeploymentMode = deploymentMode;
        target.TrafficPercentage = Math.Clamp(trafficPercentage, 0, 100);
        target.DeploymentRing = deploymentRing;
        target.RollbackOfVersionId = previousStable?.Id;
        target.EffectiveFrom = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Workflow definition {DefinitionId} for {DocumentType} deployed with mode {Mode} and traffic {TrafficPercentage}%.",
            definitionVersionId,
            documentType,
            deploymentMode,
            target.TrafficPercentage);

        return new WorkflowDeploymentResult
        {
            DocumentType = documentType,
            DefinitionVersionId = target.Id,
            DefinitionVersion = target.Version,
            DeploymentMode = target.DeploymentMode,
            TrafficPercentage = target.TrafficPercentage,
            DeploymentRing = target.DeploymentRing,
            PreviousStableVersionId = previousStable?.Id
        };
    }

    public async Task<WorkflowDeploymentResult?> RollbackAsync(
        string documentType,
        int activeDefinitionVersionId,
        CancellationToken cancellationToken = default)
    {
        var versions = await _context.WorkflowDefinitionVersions
            .Where(item => item.DocumentType == documentType)
            .OrderByDescending(item => item.Version)
            .ToListAsync(cancellationToken);

        var active = versions.FirstOrDefault(item => item.Id == activeDefinitionVersionId);
        if (active == null)
        {
            return null;
        }

        var rollbackTargetId = active.RollbackOfVersionId;
        if (!rollbackTargetId.HasValue)
        {
            rollbackTargetId = versions
                .Where(item => item.Id != activeDefinitionVersionId &&
                               string.Equals(item.DeploymentMode, WorkflowDeploymentMode.Stable, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(item => item.Version)
                .Select(item => (int?)item.Id)
                .FirstOrDefault();
        }

        if (!rollbackTargetId.HasValue)
        {
            rollbackTargetId = versions
                .Where(item => item.Id != activeDefinitionVersionId)
                .OrderByDescending(item => item.Version)
                .Select(item => (int?)item.Id)
                .FirstOrDefault();
        }

        if (!rollbackTargetId.HasValue)
        {
            return null;
        }

        var rollbackTarget = versions.First(item => item.Id == rollbackTargetId.Value);

        active.IsActive = false;
        active.TrafficPercentage = 0;
        rollbackTarget.IsActive = true;
        rollbackTarget.DeploymentMode = WorkflowDeploymentMode.Stable;
        rollbackTarget.TrafficPercentage = 100;
        rollbackTarget.EffectiveFrom = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return new WorkflowDeploymentResult
        {
            DocumentType = documentType,
            DefinitionVersionId = rollbackTarget.Id,
            DefinitionVersion = rollbackTarget.Version,
            DeploymentMode = rollbackTarget.DeploymentMode,
            TrafficPercentage = rollbackTarget.TrafficPercentage,
            DeploymentRing = rollbackTarget.DeploymentRing,
            PreviousStableVersionId = active.Id
        };
    }

    public WorkflowDefinitionVersion? PreviewSelectedVersion(
        string documentType,
        IReadOnlyList<WorkflowDefinitionVersion> candidates,
        int? documentId,
        string? startedByUserId)
    {
        return _selector.SelectVersion(documentType, candidates, documentId, startedByUserId);
    }

    private IReadOnlyList<WorkflowSimulationStep> SimulateScenario(WorkflowDefinitionVersion definition, WorkflowSimulationScenario scenario)
    {
        var steps = new List<WorkflowSimulationStep>();
        var ordered = definition.StepDefinitions.OrderBy(item => item.StepOrder).ToList();
        var current = ordered.FirstOrDefault();
        var guard = 0;

        while (current != null && guard < ordered.Count + 5)
        {
            guard++;
            var table = BuildDecisionTable(definition, current, "routing");
            var evaluation = _decisionEngine.Evaluate(table, scenario.Facts);
            var nextStepKey = evaluation.Outputs.TryGetValue("NextStepKey", out var output)
                ? Convert.ToString(output)
                : null;

            steps.Add(new WorkflowSimulationStep
            {
                StepKey = current.StepKey,
                StepOrder = current.StepOrder,
                SlaHours = current.SlaHours,
                EscalationHours = current.EscalationHours,
                MatchedRuleId = evaluation.MatchedRuleId,
                NextStepKey = nextStepKey,
                TimerEvents =
                [
                    new WorkflowSimulationTimerEvent { EventType = "sla.deadline", OffsetHours = current.SlaHours },
                    new WorkflowSimulationTimerEvent { EventType = "sla.escalation", OffsetHours = current.EscalationHours }
                ]
            });

            current = !string.IsNullOrWhiteSpace(nextStepKey)
                ? ordered.FirstOrDefault(item => string.Equals(item.StepKey, nextStepKey, StringComparison.OrdinalIgnoreCase))
                : ordered.FirstOrDefault(item => item.StepOrder > current.StepOrder);
        }

        return steps;
    }

    private static DecisionTableDefinition BuildDecisionTable(WorkflowDefinitionVersion definition, WorkflowStepDefinition stepDefinition, string purpose)
    {
        return new DecisionTableDefinition
        {
            TableKey = $"{definition.DocumentType}:{stepDefinition.StepKey}:{purpose}",
            VersionTag = $"{definition.Id}:v{definition.Version}",
            Rules = stepDefinition.Rules
                .OrderBy(item => item.Id)
                .Select((item, index) => new DecisionRuleDefinition
                {
                    RuleId = $"rule-{item.Id}",
                    SortOrder = index + 1,
                    FieldName = item.FieldName,
                    Operator = item.Operator,
                    Value = item.Value,
                    Outputs = new Dictionary<string, object?>
                    {
                        ["NextStepKey"] = item.NextStepKey,
                        ["AssigneeUserId"] = item.AssigneeUserId,
                        ["AssigneeRoleId"] = item.AssigneeRoleId,
                        ["AssigneeDepartmentId"] = item.AssigneeDepartmentId
                    }
                })
                .ToList()
        };
    }
}

public sealed class WorkflowSimulationScenario
{
    public string ScenarioId { get; init; } = string.Empty;
    public IReadOnlyDictionary<string, object?> Facts { get; init; } = new Dictionary<string, object?>();
}

public sealed class WorkflowSimulationReport
{
    public string DocumentType { get; init; } = string.Empty;
    public int DefinitionVersionId { get; init; }
    public int DefinitionVersion { get; init; }
    public DateTimeOffset GeneratedAt { get; init; }
    public IReadOnlyList<WorkflowSimulationPath> Paths { get; init; } = [];
}

public sealed class WorkflowSimulationPath
{
    public string ScenarioId { get; init; } = string.Empty;
    public bool HasLoop { get; init; }
    public int TotalSlaHours { get; init; }
    public int TotalEscalationHours { get; init; }
    public IReadOnlyList<WorkflowSimulationStep> Steps { get; init; } = [];
}

public sealed class WorkflowSimulationStep
{
    public string StepKey { get; init; } = string.Empty;
    public int StepOrder { get; init; }
    public int SlaHours { get; init; }
    public int EscalationHours { get; init; }
    public string? MatchedRuleId { get; init; }
    public string? NextStepKey { get; init; }
    public IReadOnlyList<WorkflowSimulationTimerEvent> TimerEvents { get; init; } = [];
}

public sealed class WorkflowSimulationTimerEvent
{
    public string EventType { get; init; } = string.Empty;
    public int OffsetHours { get; init; }
}

public sealed class WorkflowDeploymentResult
{
    public string DocumentType { get; init; } = string.Empty;
    public int DefinitionVersionId { get; init; }
    public int DefinitionVersion { get; init; }
    public string DeploymentMode { get; init; } = WorkflowDeploymentMode.Stable;
    public int TrafficPercentage { get; init; }
    public string? DeploymentRing { get; init; }
    public int? PreviousStableVersionId { get; init; }
}
