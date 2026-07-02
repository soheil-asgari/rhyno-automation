using System.Text.Json.Serialization;

namespace OfficeAutomation.Services.Decisioning;

public sealed class DecisionTableDefinition
{
    public string TableKey { get; init; } = string.Empty;
    public string VersionTag { get; init; } = string.Empty;
    public string? Name { get; init; }
    public string HitPolicy { get; init; } = "first";
    public IReadOnlyList<DecisionRuleDefinition> Rules { get; init; } = [];
}

public sealed class DecisionRuleDefinition
{
    public string RuleId { get; init; } = string.Empty;
    public int SortOrder { get; init; }
    public string FieldName { get; init; } = string.Empty;
    public string Operator { get; init; } = "eq";
    public string? Value { get; init; }
    public IReadOnlyDictionary<string, object?> Outputs { get; init; } = new Dictionary<string, object?>();
}

public sealed class DecisionEvaluationResult
{
    public string TableKey { get; init; } = string.Empty;
    public string VersionTag { get; init; } = string.Empty;
    public string HitPolicy { get; init; } = "first";
    public bool Matched { get; init; }
    public string? MatchedRuleId { get; init; }
    public IReadOnlyDictionary<string, object?> Outputs { get; init; } = new Dictionary<string, object?>();
    public IReadOnlyList<DecisionRuleTrace> Trace { get; init; } = [];
}

public sealed class DecisionRuleTrace
{
    public string RuleId { get; init; } = string.Empty;
    public string FieldName { get; init; } = string.Empty;
    public string Operator { get; init; } = string.Empty;
    public string? ExpectedValue { get; init; }
    public string? ActualValue { get; init; }
    public bool Matched { get; init; }
    public string Reason { get; init; } = string.Empty;
}

public sealed class DecisionRegressionCase
{
    public string CaseId { get; init; } = string.Empty;
    public IReadOnlyDictionary<string, object?> Facts { get; init; } = new Dictionary<string, object?>();
    public string? ExpectedRuleId { get; init; }
    public IReadOnlyDictionary<string, object?> ExpectedOutputs { get; init; } = new Dictionary<string, object?>();
}

public sealed class DecisionRegressionCaseResult
{
    public string CaseId { get; init; } = string.Empty;
    public bool Passed { get; init; }
    public string? FailureReason { get; init; }
    public DecisionEvaluationResult ActualResult { get; init; } = new();
}

public sealed class DecisionRegressionReport
{
    public string TableKey { get; init; } = string.Empty;
    public string VersionTag { get; init; } = string.Empty;
    public int TotalCases { get; init; }
    public int PassedCases { get; init; }
    public int FailedCases { get; init; }
    public IReadOnlyList<DecisionRegressionCaseResult> Results { get; init; } = [];
}

public sealed class DecisionExplanationEnvelope
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DecisionContext { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DefinitionVersionTag { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DecisionEvaluationResult? RoutingDecision { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DecisionEvaluationResult? AssignmentDecision { get; init; }
}
