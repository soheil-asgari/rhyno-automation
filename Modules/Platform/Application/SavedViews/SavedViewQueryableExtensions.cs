using System.Globalization;
using System.Linq.Expressions;
using System.Text.Json;

namespace OfficeAutomation.Modules.Platform.Application.SavedViews;

public static class SavedViewQueryableExtensions
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly HashSet<string> SupportedOperators = new(StringComparer.OrdinalIgnoreCase)
    {
        "eq",
        "neq",
        "gt",
        "gte",
        "lt",
        "lte",
        "contains",
        "startswith",
        "endswith"
    };

    public static IQueryable<T> ApplySavedView<T>(
        this IQueryable<T> query,
        string? filterQueryJson,
        IEnumerable<string>? allowedFields = null)
    {
        if (string.IsNullOrWhiteSpace(filterQueryJson) || filterQueryJson.Trim() == "{}")
        {
            return query;
        }

        var root = JsonSerializer.Deserialize<SavedViewFilterNode>(filterQueryJson, JsonOptions);
        if (root == null)
        {
            return query;
        }

        var allowedFieldSet = allowedFields == null
            ? null
            : new HashSet<string>(allowedFields, StringComparer.OrdinalIgnoreCase);

        var parameter = Expression.Parameter(typeof(T), "item");
        var body = BuildExpression(parameter, root, allowedFieldSet);
        if (body == null)
        {
            return query;
        }

        return query.Where(Expression.Lambda<Func<T, bool>>(body, parameter));
    }

    private static Expression? BuildExpression(
        ParameterExpression parameter,
        SavedViewFilterNode node,
        HashSet<string>? allowedFields)
    {
        if (node.Filters is { Count: > 0 })
        {
            var expressions = node.Filters
                .Select(child => BuildExpression(parameter, child, allowedFields))
                .Where(expression => expression != null)
                .Cast<Expression>()
                .ToList();

            if (expressions.Count == 0)
            {
                return null;
            }

            return expressions.Aggregate((left, right) =>
                string.Equals(node.Logic, "or", StringComparison.OrdinalIgnoreCase)
                    ? Expression.OrElse(left, right)
                    : Expression.AndAlso(left, right));
        }

        if (string.IsNullOrWhiteSpace(node.Field) || string.IsNullOrWhiteSpace(node.Operator))
        {
            return null;
        }

        if (!SupportedOperators.Contains(node.Operator))
        {
            throw new InvalidOperationException($"Saved view operator '{node.Operator}' is not supported.");
        }

        if (allowedFields != null && !allowedFields.Contains(node.Field))
        {
            throw new InvalidOperationException($"Saved view field '{node.Field}' is not allowed for this grid.");
        }

        var member = BuildMemberAccess(parameter, node.Field);
        var targetType = Nullable.GetUnderlyingType(member.Type) ?? member.Type;
        var constant = Expression.Constant(ConvertValue(node.Value, targetType), targetType);
        var left = member.Type == targetType ? member : Expression.Convert(member, targetType);

        return node.Operator.ToLowerInvariant() switch
        {
            "eq" => Expression.Equal(left, constant),
            "neq" => Expression.NotEqual(left, constant),
            "gt" => Expression.GreaterThan(left, constant),
            "gte" => Expression.GreaterThanOrEqual(left, constant),
            "lt" => Expression.LessThan(left, constant),
            "lte" => Expression.LessThanOrEqual(left, constant),
            "contains" => BuildStringCall(left, nameof(string.Contains), constant),
            "startswith" => BuildStringCall(left, nameof(string.StartsWith), constant),
            "endswith" => BuildStringCall(left, nameof(string.EndsWith), constant),
            _ => throw new InvalidOperationException($"Saved view operator '{node.Operator}' is not supported.")
        };
    }

    private static Expression BuildMemberAccess(Expression root, string field)
    {
        return field.Split('.', StringSplitOptions.RemoveEmptyEntries)
            .Aggregate(root, Expression.PropertyOrField);
    }

    private static Expression BuildStringCall(Expression member, string methodName, ConstantExpression value)
    {
        if (member.Type != typeof(string))
        {
            throw new InvalidOperationException($"Operator '{methodName}' can only be applied to string fields.");
        }

        var method = typeof(string).GetMethod(methodName, [typeof(string)])
            ?? throw new InvalidOperationException($"String method '{methodName}' was not found.");

        return Expression.AndAlso(
            Expression.NotEqual(member, Expression.Constant(null, typeof(string))),
            Expression.Call(member, method, Expression.Convert(value, typeof(string))));
    }

    private static object? ConvertValue(object? value, Type targetType)
    {
        if (value is JsonElement json)
        {
            return ConvertJsonElement(json, targetType);
        }

        if (value == null)
        {
            return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;
        }

        if (targetType.IsEnum)
        {
            return Enum.Parse(targetType, value.ToString() ?? string.Empty, ignoreCase: true);
        }

        return Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
    }

    private static object? ConvertJsonElement(JsonElement json, Type targetType)
    {
        if (targetType == typeof(string))
        {
            return json.ValueKind == JsonValueKind.String ? json.GetString() : json.ToString();
        }

        if (targetType == typeof(Guid))
        {
            return json.ValueKind == JsonValueKind.String ? Guid.Parse(json.GetString()!) : Guid.Parse(json.ToString());
        }

        if (targetType == typeof(DateTime))
        {
            return json.GetDateTime();
        }

        if (targetType == typeof(DateTimeOffset))
        {
            return json.GetDateTimeOffset();
        }

        if (targetType == typeof(bool))
        {
            return json.GetBoolean();
        }

        if (targetType == typeof(int))
        {
            return json.GetInt32();
        }

        if (targetType == typeof(long))
        {
            return json.GetInt64();
        }

        if (targetType == typeof(decimal))
        {
            return json.GetDecimal();
        }

        if (targetType == typeof(double))
        {
            return json.GetDouble();
        }

        if (targetType.IsEnum)
        {
            return Enum.Parse(targetType, json.GetString() ?? json.ToString(), ignoreCase: true);
        }

        return JsonSerializer.Deserialize(json.GetRawText(), targetType, JsonOptions);
    }
}
