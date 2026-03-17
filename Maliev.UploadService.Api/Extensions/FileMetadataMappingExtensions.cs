using Maliev.UploadService.Api.Models.Responses;
using Maliev.UploadService.Domain.Entities;

namespace Maliev.UploadService.Api.Extensions;

/// <summary>
/// Extension methods for mapping FileMetadata entities to responses.
/// </summary>
public static class FileMetadataMappingExtensions
{
    /// <summary>
    /// Converts a FileMetadata entity to a FileMetadataResponse.
    /// </summary>
    /// <param name="fileMetadata">The file metadata entity.</param>
    /// <returns>The file metadata response.</returns>
    public static FileMetadataResponse ToResponse(this FileMetadata fileMetadata)
    {
        return new FileMetadataResponse
        {
            FileId = fileMetadata.FileId,
            UploadId = fileMetadata.UploadId,
            ServiceId = fileMetadata.ServiceId,
            StoragePath = fileMetadata.StoragePath,
            VersionETag = fileMetadata.VersionETag,
            FileSize = fileMetadata.FileSize,
            ContentType = fileMetadata.ContentType,
            Checksum = fileMetadata.Checksum,
            UploadedAt = fileMetadata.UploadedAt,
            LastAccessedAt = fileMetadata.LastAccessedAt,
            StorageClass = fileMetadata.StorageClass,
            ExpiresAt = fileMetadata.ExpiresAt,
            Metadata = fileMetadata.Metadata
        };
    }
}
