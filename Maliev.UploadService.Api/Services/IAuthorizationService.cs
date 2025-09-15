using Maliev.UploadService.Data.Models;

namespace Maliev.UploadService.Api.Services;

public interface IAuthorizationService
{
    Task<bool> CanAccessFileAsync(Guid fileId, string userId, string[] userRoles);
    Task<bool> CanUploadToCategory(string category, string userId, string[] userRoles);
    Task<bool> CanDeleteFileAsync(Guid fileId, string userId, string[] userRoles);
    Task<bool> IsFileOwnerAsync(Guid fileId, string userId);
    Task<AccessLevel> GetMaximumAccessLevelAsync(string userId, string[] userRoles);
}