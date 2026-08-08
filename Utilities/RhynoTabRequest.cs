using Microsoft.AspNetCore.Http;

namespace OfficeAutomation.Utilities;

/// <summary>
/// Detects whether an HTTP request is an AJAX or Rhyno/Rahkaran inner-tab load.
/// When a Tax module page is loaded inside an ERP workspace tab we must render the
/// view without the outer shell layout so it integrates seamlessly into the tab
/// container (see <c>Views/_ViewStart.cshtml</c> and <c>Views/_TabLayout.cshtml</c>).
/// </summary>
public static class RhynoTabRequest
{
    /// <summary>
    /// Returns <c>true</c> when the request is a standard AJAX request
    /// (<c>X-Requested-With: XMLHttpRequest</c>) or a Rhyno/Rahkaran inner-tab load
    /// signalled by the <c>_tab</c> query parameter or a dedicated tab header.
    /// </summary>
    public static bool IsRhynoTabOrAjax(this HttpRequest request)
    {
        if (request.Query.ContainsKey("_tab"))
        {
            return true;
        }

        var requestedWith = request.Headers["X-Requested-With"].ToString();
        if (string.Equals(requestedWith, "XMLHttpRequest", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return request.Headers.ContainsKey("X-Rhyno-Tab")
            || request.Headers.ContainsKey("X-Rahkaran-Tab")
            || request.Headers.ContainsKey("X-Tab");
    }
}