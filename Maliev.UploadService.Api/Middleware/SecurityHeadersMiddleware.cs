namespace Maliev.UploadService.Api.Middleware;

public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<SecurityHeadersMiddleware> _logger;

    public SecurityHeadersMiddleware(RequestDelegate next, ILogger<SecurityHeadersMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Add security headers
        var response = context.Response;

        // Content Security Policy - Prevent XSS attacks
        response.Headers["Content-Security-Policy"] =
            "default-src 'self'; " +
            "script-src 'self' 'unsafe-inline' 'unsafe-eval'; " +
            "style-src 'self' 'unsafe-inline'; " +
            "img-src 'self' data: https:; " +
            "font-src 'self' https:; " +
            "connect-src 'self'; " +
            "frame-ancestors 'none';";

        // Prevent clickjacking
        response.Headers["X-Frame-Options"] = "DENY";

        // Prevent MIME type sniffing
        response.Headers["X-Content-Type-Options"] = "nosniff";

        // XSS Protection
        response.Headers["X-XSS-Protection"] = "1; mode=block";

        // Referrer Policy
        response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

        // Permissions Policy (formerly Feature Policy)
        response.Headers["Permissions-Policy"] =
            "camera=(), " +
            "microphone=(), " +
            "geolocation=(), " +
            "payment=(), " +
            "usb=()";

        // HSTS (only for HTTPS)
        if (context.Request.IsHttps)
        {
            response.Headers["Strict-Transport-Security"] =
                "max-age=31536000; includeSubDomains; preload";
        }

        // Remove server information
        response.Headers.Remove("Server");
        response.Headers.Remove("X-Powered-By");
        response.Headers.Remove("X-AspNet-Version");
        response.Headers.Remove("X-AspNetMvc-Version");

        await _next(context);
    }
}

// Extension method to register the middleware
public static class SecurityHeadersMiddlewareExtensions
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<SecurityHeadersMiddleware>();
    }
}