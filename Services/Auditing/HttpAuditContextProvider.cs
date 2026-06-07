using System.Security.Claims;

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
                return new AuditRequestInfo(null, null, null);
            }

            var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userIp = httpContext.Connection.RemoteIpAddress?.ToString();
            var userAgent = httpContext.Request.Headers.UserAgent.ToString();

            return new AuditRequestInfo(
                string.IsNullOrWhiteSpace(userId) ? null : userId,
                string.IsNullOrWhiteSpace(userIp) ? null : userIp,
                string.IsNullOrWhiteSpace(userAgent) ? null : userAgent);
        }
    }
}
