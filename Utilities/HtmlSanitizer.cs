using Ganss.Xss;

namespace OfficeAutomation.Utilities;

public static class HtmlSanitizer
{
    private static readonly Lazy<Ganss.Xss.HtmlSanitizer> SanitizerInstance = new(() =>
    {
        var sanitizer = new Ganss.Xss.HtmlSanitizer();

        // Allowed safe formatting tags: p, strong, em, ul, ol, li, table, thead, tbody, tr, th, td, br, span, b, i, u, h1-h6, hr, etc.
        sanitizer.AllowedTags.Clear();
        var allowedTags = new[]
        {
            "p", "strong", "em", "b", "i", "u", "s", "strike",
            "ul", "ol", "li",
            "table", "thead", "tbody", "tfoot", "tr", "th", "td",
            "br", "hr", "span", "div", "blockquote", "pre", "code",
            "h1", "h2", "h3", "h4", "h5", "h6"
        };
        foreach (var tag in allowedTags)
        {
            sanitizer.AllowedTags.Add(tag);
        }

        // Allowed attributes: style for basic formatting, class, title, dir, align, colspan, rowspan
        sanitizer.AllowedAttributes.Clear();
        var allowedAttributes = new[]
        {
            "style", "class", "dir", "align", "title",
            "colspan", "rowspan", "border", "cellpadding", "cellspacing", "width"
        };
        foreach (var attr in allowedAttributes)
        {
            sanitizer.AllowedAttributes.Add(attr);
        }

        // Allowed CSS styling properties for basic formatting
        sanitizer.AllowedCssProperties.Clear();
        var allowedCssProperties = new[]
        {
            "color", "background-color", "text-align", "font-weight", "font-style",
            "text-decoration", "font-family", "font-size", "line-height",
            "margin", "margin-top", "margin-bottom", "margin-left", "margin-right",
            "padding", "padding-top", "padding-bottom", "padding-left", "padding-right",
            "border", "border-collapse", "border-color", "border-style", "border-width",
            "width", "min-width", "max-width", "direction"
        };
        foreach (var prop in allowedCssProperties)
        {
            sanitizer.AllowedCssProperties.Add(prop);
        }

        // Strip dangerous schemes - ensure only safe protocols
        sanitizer.AllowedSchemes.Clear();
        sanitizer.AllowedSchemes.Add("http");
        sanitizer.AllowedSchemes.Add("https");
        sanitizer.AllowedSchemes.Add("mailto");

        return sanitizer;
    });

    public static string Sanitize(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        return SanitizerInstance.Value.Sanitize(html).Trim();
    }

    public static string StripTags(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        var sanitizer = new Ganss.Xss.HtmlSanitizer();
        sanitizer.AllowedTags.Clear();
        sanitizer.AllowedAttributes.Clear();
        sanitizer.AllowedCssProperties.Clear();
        sanitizer.AllowedSchemes.Clear();

        return System.Net.WebUtility.HtmlDecode(sanitizer.Sanitize(html)).Trim();
    }
}

