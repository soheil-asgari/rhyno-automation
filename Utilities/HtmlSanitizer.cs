using System.Net;
using System.Text.RegularExpressions;

namespace OfficeAutomation.Utilities;

public static class HtmlSanitizer
{
    private static readonly Regex ScriptBlock = new(@"<\s*(script|style|iframe|object|embed|form|input|button|textarea|select|option)[^>]*>.*?<\s*/\s*\1\s*>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex SelfClosingDangerous = new(@"<\s*(script|style|iframe|object|embed|form|input|button|textarea|select|option)[^>]*\/?\s*>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex EventAttributes = new(@"\son\w+\s*=\s*(""(?:[^""]*)""|'(?:[^']*)'|[^\s>]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex JsProtocol = new(@"(href|src)\s*=\s*(""(?:javascript|data):[^""]*""|'(?:javascript|data):[^']*'|(?:javascript|data):[^\s>]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex EmptyParagraphs = new(@"<p>(\s|&nbsp;)*</p>", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static string Sanitize(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        var sanitized = html;
        sanitized = ScriptBlock.Replace(sanitized, string.Empty);
        sanitized = SelfClosingDangerous.Replace(sanitized, string.Empty);
        sanitized = EventAttributes.Replace(sanitized, string.Empty);
        sanitized = JsProtocol.Replace(sanitized, "$1=\"#\"");
        sanitized = EmptyParagraphs.Replace(sanitized, string.Empty);
        return sanitized.Trim();
    }

    public static string StripTags(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        var text = Regex.Replace(html, "<.*?>", " ");
        return WebUtility.HtmlDecode(Regex.Replace(text, @"\s+", " ").Trim());
    }
}
