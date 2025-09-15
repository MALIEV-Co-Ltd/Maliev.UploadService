using Maliev.UploadService.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Security.Claims;

namespace Maliev.UploadService.Api.Attributes;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class AuthorizeFileAccessAttribute : ActionFilterAttribute
{
    private readonly string _fileIdParameterName;
    private readonly string _action;

    public AuthorizeFileAccessAttribute(string fileIdParameterName = "fileId", string action = "read")
    {
        _fileIdParameterName = fileIdParameterName;
        _action = action.ToLowerInvariant();
    }

    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        // In Testing environment, skip authorization
        var environment = context.HttpContext.RequestServices.GetService<IWebHostEnvironment>();
        if (environment?.IsEnvironment("Testing") == true)
        {
            await next();
            return;
        }

        var authorizationService = context.HttpContext.RequestServices.GetService<IAuthorizationService>();
        if (authorizationService == null)
        {
            context.Result = new StatusCodeResult(500);
            return;
        }

        // Extract user information
        var user = context.HttpContext.User;
        var userId = GetUserIdFromClaims(user);
        var userRoles = GetUserRolesFromClaims(user);

        // Extract file ID from route or query parameters
        if (!context.ActionArguments.TryGetValue(_fileIdParameterName, out var fileIdObj) ||
            !Guid.TryParse(fileIdObj?.ToString(), out var fileId))
        {
            context.Result = new BadRequestObjectResult(new { error = $"Invalid or missing {_fileIdParameterName}" });
            return;
        }

        // Check authorization based on action
        bool isAuthorized = _action switch
        {
            "read" or "download" => await authorizationService.CanAccessFileAsync(fileId, userId, userRoles),
            "delete" => await authorizationService.CanDeleteFileAsync(fileId, userId, userRoles),
            _ => await authorizationService.CanAccessFileAsync(fileId, userId, userRoles)
        };

        if (!isAuthorized)
        {
            context.Result = new ForbidResult();
            return;
        }

        await next();
    }

    private static string GetUserIdFromClaims(ClaimsPrincipal user)
    {
        return user.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
               user.FindFirst("sub")?.Value ??
               user.Identity?.Name ??
               "anonymous";
    }

    private static string[] GetUserRolesFromClaims(ClaimsPrincipal user)
    {
        return user.FindAll(ClaimTypes.Role)
            .Select(c => c.Value)
            .ToArray();
    }
}