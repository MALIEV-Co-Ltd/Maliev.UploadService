using Maliev.UploadService.Api.Models.Responses;
using Maliev.UploadService.Domain.Entities;

namespace Maliev.UploadService.Api.Extensions;

/// <summary>
/// Extension methods for mapping Upload entities to responses.
/// </summary>
public static class UploadMappingExtensions
{
    /// <summary>
    /// Converts an Upload entity to an UploadResponse.
    /// </summary>
    /// <param name="upload">The upload entity.</param>
    /// <param name="signedUrl">Optional signed URL for download.</param>
    /// <returns>The upload response.</returns>
    public static UploadResponse ToResponse(this Upload upload, string? signedUrl = null)
    {
        return new UploadResponse
        {
            UploadId = upload.UploadId,
            ServiceId = upload.ServiceId,
            FileName = upload.FileName,
            ContentType = upload.ContentType,
            FileSize = upload.FileSize,
            Checksum = upload.Checksum,
            StoragePath = upload.StoragePath,
            Status = upload.Status.ToString(),
            UploadedAt = upload.UploadedAt,
            CompletedAt = upload.CompletedAt,
            SignedUrl = signedUrl
        };
    }
}
