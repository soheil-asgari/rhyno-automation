using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace OfficeAutomation.Services.Decisioning;

public interface IDecisionEngine
{
    DecisionEvaluationResult Evaluate(DecisionTableDefinition table, IReadOnlyDictionary<string, object?> facts);
    DecisionRegressionReport RunRegression(DecisionTableDefinition table, IEnumerable<DecisionRegressionCase> cases);
}

public sealed class DecisionEngine : IDecisionEngine
{
    private readonly ILogger<DecisionEngine> _logger;

    public DecisionEngine(ILogger<DecisionEngine>? logger = null)
    {
        _logger = logger ?? NullLogger<DecisionEngine>.Instance;
    }

    public DecisionEvaluationResult Evaluate(DecisionTableDefinition table, IReadOnlyDictionary<string, object?> facts)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(facts);

        var trace = new List<DecisionRuleTrace>();
        DecisionRuleDefinition? matchedRule = null;

        foreach (var rule in table.Rules.OrderBy(item => item.SortOrder))
        {
            var match = EvaluateRule(rule, facts);
            trace.Add(match);
            if (match.Matched)
            {
                matchedRule = rule;
                break;
            }
        }

        var result = new DecisionEvaluationResult
        {
            TableKey = table.TableKey,
            VersionTag = table.VersionTag,
            HitPolicy = table.HitPolicy,
            Matched = matchedRule != null,
            MatchedRuleId = matchedRule?.RuleId,
            Outputs = matchedRule?.Outputs ?? new Dictionary<string, object?>(),
            Trace = trace
        };

        _logger.LogInformation(
            "Decision table {TableKey} version {VersionTag} evaluated. Matched={Matched} Rule={MatchedRuleId} Facts={Facts}",
            result.TableKey,
            result.VersionTag,
            result.Matched,
            result.MatchedRuleId,
            JsonSerializer.Serialize(facts));

        return result;
    }

    public DecisionRegressionReport RunRegression(DecisionTableDefinition table, IEnumerable<DecisionRegressionCase> cases)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(cases);

        var results = new List<DecisionRegressionCaseResult>();

        foreach (var testCase in cases)
        {
            var actual = Evaluate(table, testCase.Facts);
            var passed = MatchesExpectation(testCase, actual, out var failureReason);
            results.Add(new DecisionRegressionCaseResult
            {
                CaseId = testCase.CaseId,
                Passed = passed,
                FailureReason = failureReason,
                ActualResult = actual
            });
        }

        return new DecisionRegressionReport
        {
            TableKey = table.TableKey,
            VersionTag = table.VersionTag,
            TotalCases = results.Count,
            PassedCases = results.Count(item => item.Passed),
            FailedCases = results.Count(item => !item.Passed),
            Results = results
        };
    }

    private static DecisionRuleTrace EvaluateRule(DecisionRuleDefinition rule, IReadOnlyDictionary<string, object?> facts)
    {
        if (!facts.TryGetValue(rule.FieldName, out var factValue))
        {
            return new DecisionRuleTrace
            {
                RuleId = rule.RuleId,
                FieldName = rule.FieldName,
                Operator = rule.Operator,
                ExpectedValue = rule.Value,
                ActualValue = null,
                Matched = false,
                Reason = "Fact was not present."
            };
        }

        var op = rule.Operator?.Trim().ToLowerInvariant() ?? "eq";
        var matched = op switch
        {
            "eq" or "=" or "==" => CompareEquals(factValue, rule.Value),
            "neq" or "!=" or "<>" => !CompareEquals(factValue, rule.Value),
            "contains" => ConvertToString(factValue)?.Contains(rule.Value ?? string.Empty, StringComparison.OrdinalIgnoreCase) == true,
            "startswith" => ConvertToString(factValue)?.StartsWith(rule.Value ?? string.Empty, StringComparison.OrdinalIgnoreCase) == true,
            "endswith" => ConvertToString(factValue)?.EndsWith(rule.Value ?? string.Empty, StringComparison.OrdinalIgnoreCase) == true,
            "in" => SplitValues(rule.Value).Contains(ConvertToString(factValue) ?? string.Empty, StringComparer.OrdinalIgnoreCase),
            "gt" or ">" => CompareNumeric(factValue, rule.Value, comparison => comparison > 0),
            "gte" or ">=" => CompareNumeric(factValue, rule.Value, comparison => comparison >= 0),
            "lt" or "<" => CompareNumeric(factValue, rule.Value, comparison => comparison < 0),
            "lte" or "<=" => CompareNumeric(factValue, rule.Value, comparison => comparison <= 0),
            "isnull" => factValue == null,
            "notnull" => factValue != null,
            _ => false
        };

        return new DecisionRuleTrace
        {
            RuleId = rule.RuleId,
            FieldName = rule.FieldName,
            Operator = op,
            ExpectedValue = rule.Value,
            ActualValue = ConvertToString(factValue),
            Matched = matched,
            Reason = matched ? "Rule condition matched." : "Rule condition did not match."
        };
    }

    private static bool MatchesExpectation(DecisionRegressionCase testCase, DecisionEvaluationResult actual, out string? failureReason)
    {
        if (!string.IsNullOrWhiteSpace(testCase.ExpectedRuleId) &&
            !string.Equals(testCase.ExpectedRuleId, actual.MatchedRuleId, StringComparison.OrdinalIgnoreCase))
        {
            failureReason = $"Expected rule '{testCase.ExpectedRuleId}' but got '{actual.MatchedRuleId ?? "<none>"}'.";
            return false;
        }

        foreach (var expected in testCase.ExpectedOutputs)
        {
            actual.Outputs.TryGetValue(expected.Key, out var actualValue);
            if (!CompareEquals(actualValue, ConvertToString(expected.Value)))
            {
                failureReason = $"Expected output '{expected.Key}' to be '{expected.Value}' but got '{actualValue}'.";
                return false;
            }
        }

        failureReason = null;
        return true;
    }

    private static string? ConvertToString(object? value)
    {
        return Convert.ToString(value, CultureInfo.InvariantCulture);
    }

    private static bool CompareEquals(object? left, string? right)
    {
        var leftText = ConvertToString(left);
        return string.Equals(leftText, right, StringComparison.OrdinalIgnoreCase);
    }

    private static bool CompareNumeric(object? left, string? right, Func<int, bool> matcher)
    {
        if (left == null || string.IsNullOrWhiteSpace(right))
        {
            return false;
        }

        if (!decimal.TryParse(ConvertToString(left), NumberStyles.Any, CultureInfo.InvariantCulture, out var leftValue) ||
            !decimal.TryParse(right, NumberStyles.Any, CultureInfo.InvariantCulture, out var rightValue))
        {
            return false;
        }

        return matcher(decimal.Compare(leftValue, rightValue));
    }

    private static string[] SplitValues(string? raw)
    {
        return string.IsNullOrWhiteSpace(raw)
            ? []
            : raw.Split([',', ';', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
