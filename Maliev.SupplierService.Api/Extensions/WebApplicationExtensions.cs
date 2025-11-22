using Maliev.SupplierService.Api.Middleware;

namespace Maliev.SupplierService.Api.Extensions;

public static class WebApplicationExtensions
{
    public static WebApplication UseSupplierServiceMiddleware(this WebApplication app)
    {
        // Exception handling should be first
        app.UseMiddleware<ExceptionHandlingMiddleware>();

        // Request logging
        app.UseMiddleware<RequestLoggingMiddleware>();

        return app;
    }
}
