using Maliev.UploadService.Data.DbContexts;
using Maliev.UploadService.Data.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Maliev.UploadService.Api.Services;

public class AuthorizationService : IAuthorizationService
{
    private readonly UploadDbContext _context;
    private readonly ILogger<AuthorizationService> _logger;

    // Role hierarchy - higher numbers indicate higher privileges
    private static readonly Dictionary<string, int> RoleHierarchy = new()
    {
        ["SuperAdmin"] = 100,
        ["Admin"] = 90,
        ["Manager"] = 80,
        ["Finance"] = 70,
        ["Sales"] = 60,
        ["Production"] = 50,
        ["Customer"] = 40,
        ["Guest"] = 10
    };

    public AuthorizationService(UploadDbContext context, ILogger<AuthorizationService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<bool> CanAccessFileAsync(Guid fileId, string userId, string[] userRoles)
    {
        try
        {
            var file = await _context.UploadedFiles
                .AsNoTracking()
                .FirstOrDefaultAsync(f => f.Id == fileId && !f.IsDeleted);

            if (file == null)
                return false;

            // SuperAdmin can access everything
            if (userRoles.Contains("SuperAdmin"))
                return true;

            // File owner can always access their files
            if (file.UploadedBy == userId)
                return true;

            // Check access level based on user roles
            return file.AccessLevel switch
            {
                AccessLevel.Public => true,
                AccessLevel.Internal => HasMinimumRole(userRoles, "Guest"),
                AccessLevel.Restricted => HasMinimumRole(userRoles, "Sales"),
                AccessLevel.Confidential => HasMinimumRole(userRoles, "Manager"),
                _ => false
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking file access for user {UserId} and file {FileId}", userId, fileId);
            return false;
        }
    }

    public async Task<bool> CanUploadToCategory(string category, string userId, string[] userRoles)
    {
        // SuperAdmin can upload to any category
        if (userRoles.Contains("SuperAdmin") || userRoles.Contains("Admin"))
            return true;

        // Category-specific upload permissions
        return category.ToLowerInvariant() switch
        {
            "quotations" => HasMinimumRole(userRoles, "Sales"),
            "invoices" => HasMinimumRole(userRoles, "Finance"),
            "receipts" => HasMinimumRole(userRoles, "Finance"),
            "customers" => HasMinimumRole(userRoles, "Sales"),
            "orders" => HasMinimumRole(userRoles, "Production"),
            "products" => HasMinimumRole(userRoles, "Production"),
            "finance" => HasMinimumRole(userRoles, "Finance"),
            "marketing" => HasMinimumRole(userRoles, "Sales"),
            "legal" => HasMinimumRole(userRoles, "Manager"),
            "temp" => true, // Anyone can upload temporary files
            _ => HasMinimumRole(userRoles, "Sales") // Default minimum role
        };
    }

    public async Task<bool> CanDeleteFileAsync(Guid fileId, string userId, string[] userRoles)
    {
        try
        {
            var file = await _context.UploadedFiles
                .AsNoTracking()
                .FirstOrDefaultAsync(f => f.Id == fileId && !f.IsDeleted);

            if (file == null)
                return false;

            // SuperAdmin can delete everything
            if (userRoles.Contains("SuperAdmin"))
                return true;

            // File owner can delete their own files
            if (file.UploadedBy == userId)
                return true;

            // Managers can delete non-confidential files
            if (HasMinimumRole(userRoles, "Manager") && file.AccessLevel != AccessLevel.Confidential)
                return true;

            // Admin can delete everything except SuperAdmin uploads
            if (userRoles.Contains("Admin"))
            {
                // Check if the file was uploaded by a SuperAdmin
                var uploaderRoles = await GetUserRolesAsync(file.UploadedBy);
                return !uploaderRoles.Contains("SuperAdmin");
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking delete permissions for user {UserId} and file {FileId}", userId, fileId);
            return false;
        }
    }

    public async Task<bool> IsFileOwnerAsync(Guid fileId, string userId)
    {
        try
        {
            return await _context.UploadedFiles
                .AnyAsync(f => f.Id == fileId && f.UploadedBy == userId && !f.IsDeleted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking file ownership for user {UserId} and file {FileId}", userId, fileId);
            return false;
        }
    }

    public async Task<AccessLevel> GetMaximumAccessLevelAsync(string userId, string[] userRoles)
    {
        // Determine maximum access level based on user roles
        if (userRoles.Contains("SuperAdmin") || userRoles.Contains("Admin"))
            return AccessLevel.Confidential;

        if (HasMinimumRole(userRoles, "Manager"))
            return AccessLevel.Confidential;

        if (HasMinimumRole(userRoles, "Finance"))
            return AccessLevel.Restricted;

        if (HasMinimumRole(userRoles, "Sales") || HasMinimumRole(userRoles, "Production"))
            return AccessLevel.Internal;

        return AccessLevel.Public; // Guest, Customer, or no roles
    }

    // Private helper methods

    private static bool HasMinimumRole(string[] userRoles, string requiredRole)
    {
        if (userRoles == null || userRoles.Length == 0)
            return false; // Users with no roles cannot access files requiring roles

        var requiredLevel = RoleHierarchy.GetValueOrDefault(requiredRole, 0);
        return userRoles.Any(role => RoleHierarchy.GetValueOrDefault(role, 0) >= requiredLevel);
    }

    private static bool IsCustomerFile(UploadedFile file, string userId)
    {
        // Check if the file is associated with the customer
        // In a real scenario, you would have a mapping between userId and customerId
        return !string.IsNullOrEmpty(file.CustomerId) && file.CustomerId == userId;
    }

    private async Task<string[]> GetUserRolesAsync(string userId)
    {
        // In a real implementation, this would query your user management system
        // For now, return empty array as a placeholder
        return Array.Empty<string>();
    }
}

// Extension class for role-based authorization
public static class AuthorizationExtensions
{
    public static string GetUserIdFromClaims(this ClaimsPrincipal user)
    {
        return user.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
               user.FindFirst("sub")?.Value ??
               user.Identity?.Name ??
               "anonymous";
    }

    public static string[] GetUserRolesFromClaims(this ClaimsPrincipal user)
    {
        return user.FindAll(ClaimTypes.Role)
            .Select(c => c.Value)
            .ToArray();
    }
}