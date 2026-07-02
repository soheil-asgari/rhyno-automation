using System.Security.Claims;
using OfficeAutomation.Services.Security;

namespace OfficeAutomation.Services.Auditing
{
    public sealed class HttpAuditContextProvider : IAuditContextProvider
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public HttpAuditContextProvider(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public AuditRequestInfo GetCurrent()
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null)
            {
                return new AuditRequestInfo(null, null, null, [], [], null, null, null, null);
            }

            var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userName = httpContext.User.Identity?.Name ?? httpContext.User.FindFirstValue(ClaimTypes.Name);
            var displayName = httpContext.User.FindFirstValue(ClaimTypes.GivenName) ?? userName;
            var userIp = httpContext.Connection.RemoteIpAddress?.ToString();
            var userAgent = httpContext.Request.Headers.UserAgent.ToString();
            var roles = httpContext.User.FindAll(ClaimTypes.Role)
                .Select(item => item.Value)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return new AuditRequestInfo(
                string.IsNullOrWhiteSpace(userId) ? null : userId,
                string.IsNullOrWhiteSpace(userName) ? null : userName,
                string.IsNullOrWhiteSpace(displayName) ? null : displayName,
                roles,
                [],
                null,
                string.IsNullOrWhiteSpace(userIp) ? null : userIp,
                string.IsNullOrWhiteSpace(userAgent) ? null : userAgent,
                string.IsNullOrWhiteSpace(httpContext.TraceIdentifier) ? null : httpContext.TraceIdentifier);
        }
    }
}
