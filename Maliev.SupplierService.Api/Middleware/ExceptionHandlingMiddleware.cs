using System.Net;
using System.Text.Json;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Maliev.SupplierService.Api.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
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
        var traceId = context.TraceIdentifier;
        var (statusCode, response) = exception switch
        {
            ValidationException validationEx => (
                HttpStatusCode.BadRequest,
                CreateValidationErrorResponse(validationEx, traceId)),

            DbUpdateConcurrencyException concurrencyEx => (
                HttpStatusCode.Conflict,
                CreateErrorResponse("Conflict", $"Concurrency error: {concurrencyEx.Message}. Inner: {concurrencyEx.InnerException?.Message}", traceId)),

            KeyNotFoundException => (
                HttpStatusCode.NotFound,
                CreateErrorResponse("Not Found", exception.Message, traceId)),

            UnauthorizedAccessException => (
                HttpStatusCode.Unauthorized,
                CreateErrorResponse("Unauthorized", "Authentication is required.", traceId)),

            InvalidOperationException => (
                HttpStatusCode.BadRequest,
                CreateErrorResponse("Bad Request", exception.Message, traceId)),

            _ => (
                HttpStatusCode.InternalServerError,
                CreateErrorResponse("Internal Server Error", "An unexpected error occurred.", traceId))
        };

        if (statusCode == HttpStatusCode.InternalServerError)
        {
            _logger.LogError(exception, "Unhandled exception occurred. TraceId: {TraceId}", traceId);
        }
        else
        {
            _logger.LogWarning(exception, "Handled exception occurred. TraceId: {TraceId}", traceId);
        }

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        await context.Response.WriteAsync(JsonSerializer.Serialize(response, options));
    }

    private static object CreateErrorResponse(string title, string detail, string traceId)
    {
        return new
        {
            Type = $"https://httpstatuses.io/{GetStatusCodeFromTitle(title)}",
            Title = title,
            Status = GetStatusCodeFromTitle(title),
            Detail = detail,
            TraceId = traceId
        };
    }

    private static object CreateValidationErrorResponse(ValidationException exception, string traceId)
    {
        var errors = exception.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(
                g => g.Key,
                g => g.Select(e => e.ErrorMessage).ToArray());

        return new
        {
            Type = "https://httpstatuses.io/400",
            Title = "Validation Error",
            Status = 400,
            Detail = "One or more validation errors occurred.",
            TraceId = traceId,
            Errors = errors
        };
    }

    private static int GetStatusCodeFromTitle(string title) => title switch
    {
        "Bad Request" => 400,
        "Unauthorized" => 401,
        "Not Found" => 404,
        "Conflict" => 409,
        _ => 500
    };
}
