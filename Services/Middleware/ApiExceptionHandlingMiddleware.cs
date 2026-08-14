using System.Text.Json;

namespace OfficeAutomation.Services.Middleware;

/// <summary>
/// Returns a stable JSON error envelope for API/JSON requests when an
/// unhandled exception occurs, without leaking stack traces or internal
/// implementation details to clients. MVC (HTML) requests are passed through
/// to the existing error page pipeline.
/// </summary>
public sealed class ApiExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ApiExceptionHandlingMiddleware> _logger;
    private readonly IWebHostEnvironment _env;

    public ApiExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ApiExceptionHandlingMiddleware> logger,
        IWebHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        // For non-API (browser) requests let the standard error page / status-code
        // pipeline handle the response so the UI is preserved.
        if (!IsApiOrJsonRequest(context))
        {
            throw exception;
        }

        _logger.LogError(exception, "Unhandled exception for API request {Method} {Path}",
            context.Request.Method, context.Request.Path);

        var traceId = context.TraceIdentifier;
        var statusCode = exception switch
        {
            UnauthorizedAccessException => StatusCodes.Status403Forbidden,
            KeyNotFoundException => StatusCodes.Status404NotFound,
            InvalidOperationException => StatusCodes.Status400BadRequest,
            ArgumentException => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status500InternalServerError
        };

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json; charset=utf-8";

        var body = new
        {
            success = false,
            message = statusCode == StatusCodes.Status500InternalServerError
                ? "خطای داخلی سرور رخ داد."
                : exception.Message,
            errorCode = statusCode,
            traceId
        };

        var json = JsonSerializer.Serialize(body);
        await context.Response.WriteAsync(json, context.RequestAborted);
    }

    private static bool IsApiOrJsonRequest(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        if (path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
            return true;

        var accept = context.Request.Headers.Accept.ToString();
        if (accept.Contains("application/json", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }
}

/// <summary>
/// Extension method for easy registration.
/// </summary>
public static class ApiExceptionHandlingMiddlewareExtensions
{
    public static IApplicationBuilder UseApiExceptionHandling(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<ApiExceptionHandlingMiddleware>();
    }
}