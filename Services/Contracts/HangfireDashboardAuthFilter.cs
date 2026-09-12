using Hangfire.Dashboard;

namespace OfficeAutomation.Services;

/// <summary>
/// Authorization filter for the Hangfire dashboard.
/// Restricts access to authenticated users with the Admin role.
/// In development, all authenticated users are allowed.
/// </summary>
public class HangfireDashboardAuthFilter : IDashboardAuthorizationFilter
{
    private readonly bool _isDevelopment;

    public HangfireDashboardAuthFilter(bool isDevelopment)
    {
        _isDevelopment = isDevelopment;
    }

    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();

        // In development, allow all authenticated users
        if (_isDevelopment)
            return httpContext.User.Identity?.IsAuthenticated == true;

        // In production, require Admin role
        return httpContext.User.Identity?.IsAuthenticated == true
            && httpContext.User.IsInRole("Admin");
    }
}
