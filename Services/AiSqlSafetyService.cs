using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace OfficeAutomation.Services;

public sealed class AiSqlSafetyService
{
    private static readonly Regex UnsafeTokenPattern = new(
        @"\b(INSERT|UPDATE|DELETE|MERGE|DROP|ALTER|TRUNCATE|CREATE|EXEC|EXECUTE|GRANT|REVOKE|BACKUP|RESTORE|INTO|OPENROWSET|OPENDATASOURCE|UNION|COLLATE)\b|@@|\bSP_|\bXP_|;|--|/\*|\*/|\b(sys\.|information_schema\b)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex TopPattern = new(
        @"\bTOP\s*\(?\s*(\d+)\s*\)?",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex TableRefPattern = new(
        @"\b(?:FROM|JOIN)\s+([a-zA-Z0-9_\[\]\.]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public bool IsReadOnlySelect(string? sql)
    {
        // Backward compatibility fallback
        if (string.IsNullOrWhiteSpace(sql)) return false;
        var trimmed = sql.Trim();
        return (trimmed.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("WITH", StringComparison.OrdinalIgnoreCase)) &&
               !UnsafeTokenPattern.IsMatch(trimmed);
    }

    public bool IsSafeQuery(string? sql, IEnumerable<string> allowedTables)
    {
        if (string.IsNullOrWhiteSpace(sql)) return false;

        // 1. Check unsafe tokens
        if (UnsafeTokenPattern.IsMatch(sql)) return false;

        // 2. Check read-only select/with structure
        var trimmed = sql.Trim();
        if (!(trimmed.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase) ||
              trimmed.StartsWith("WITH", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        // 3. Enforce TOP limit <= 500
        var topMatch = TopPattern.Match(sql);
        if (!topMatch.Success)
        {
            return false;
        }
        if (int.TryParse(topMatch.Groups[1].Value, out var limit) && limit > 500)
        {
            return false;
        }

        // 4. Validate table references
        var allowedSet = new HashSet<string>(allowedTables.Select(NormalizeTableName), StringComparer.OrdinalIgnoreCase);
        var matches = TableRefPattern.Matches(sql);
        foreach (Match match in matches)
        {
            var tableRef = match.Groups[1].Value;
            var normalizedRef = NormalizeTableName(tableRef);
            if (!allowedSet.Contains(normalizedRef))
            {
                return false;
            }
        }

        return true;
    }

    private static string NormalizeTableName(string name)
    {
        var parts = name.Replace("[", "").Replace("]", "").Split('.');
        if (parts.Length == 1)
        {
            return parts[0].ToLowerInvariant();
        }
        return string.Join(".", parts).ToLowerInvariant();
    }
}
