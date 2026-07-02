using System.Text.RegularExpressions;

namespace OfficeAutomation.Services;

public sealed class AiSqlSafetyService
{
    private static readonly Regex UnsafeTokenPattern = new(
        @"\b(INSERT|UPDATE|DELETE|MERGE|DROP|ALTER|TRUNCATE|CREATE|EXEC|EXECUTE|GRANT|REVOKE|BACKUP|RESTORE|INTO|OPENROWSET|OPENDATASOURCE)\b|@@|\bSP_|\bXP_|;|--|/\*|\*/",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public bool IsReadOnlySelect(string? sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            return false;
        }

        var trimmed = sql.Trim();
        return (trimmed.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("WITH", StringComparison.OrdinalIgnoreCase)) &&
               !UnsafeTokenPattern.IsMatch(trimmed);
    }
}
