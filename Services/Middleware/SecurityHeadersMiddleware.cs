namespace OfficeAutomation.Services.Middleware;

/// <summary>
/// میان‌افزار افزودن Security Headers: CSP, HSTS, X-Frame-Options, X-Content-Type-Options و ...
/// مطابق با بهترین شیوه‌های OWASP
/// </summary>
public sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IWebHostEnvironment _env;

    public SecurityHeadersMiddleware(RequestDelegate next, IWebHostEnvironment env)
    {
        _next = next;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;

        // Content Security Policy
        headers["Content-Security-Policy"] = GetContentSecurityPolicy();

        // Prevent MIME type sniffing
        headers["X-Content-Type-Options"] = "nosniff";

        // Prevent clickjacking
        headers["X-Frame-Options"] = "DENY";

        // Enable XSS protection in older browsers
        headers["X-XSS-Protection"] = "1; mode=block";

        // Referrer policy
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

        // Permissions policy
        headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";

        // Strict Transport Security (only in production over HTTPS)
        if (!_env.IsDevelopment())
        {
            headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains; preload";
        }

        // Cross-Origin policies
        headers["Cross-Origin-Opener-Policy"] = "same-origin";
        headers["Cross-Origin-Resource-Policy"] = "same-origin";
        // COEP "require-corp" blocks cross-origin CDN resources (CKEditor, Chart.js, ...)
        // that do not send a Cross-Origin-Resource-Policy header, causing errors like
        // ERR_BLOCKED_BY_RESPONSE.NotSameOriginAfterDefaultedToSameOriginByCoep.
        // Using "unsafe-none" allows these standard CDN subresources to load normally.
        headers["Cross-Origin-Embedder-Policy"] = "unsafe-none";

        // Remove server header
        headers.Remove("Server");
        headers.Remove("X-Powered-By");

        await _next(context);
    }

    private static string GetContentSecurityPolicy()
    {
        // CSP policy - adjust for your specific needs
        var policies = new List<string>
        {
            "default-src 'self'",
            "script-src 'self' 'unsafe-inline' 'unsafe-eval' https://cdn.jsdelivr.net https://lib.arvancloud.ir",
            "style-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net https://lib.arvancloud.ir https://fonts.googleapis.com",
            "font-src 'self' https://fonts.gstatic.com https://cdn.jsdelivr.net https://lib.arvancloud.ir",
            "img-src 'self' data: https: blob:",
            "connect-src 'self' wss: https:",
            "media-src 'self'",
            "object-src 'none'",
            "frame-src 'self'",
            "frame-ancestors 'self'",
            "form-action 'self'",
            "base-uri 'self'",
            "worker-src 'self' blob:"
        };

        return string.Join("; ", policies);
    }
}

/// <summary>
/// Extension method for easy registration
/// </summary>
public static class SecurityHeadersMiddlewareExtensions
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<SecurityHeadersMiddleware>();
    }
}