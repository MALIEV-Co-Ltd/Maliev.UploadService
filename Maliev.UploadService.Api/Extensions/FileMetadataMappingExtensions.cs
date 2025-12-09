using Maliev.UploadService.Api.Models.Entities;
using Maliev.UploadService.Api.Models.Responses;

namespace Maliev.UploadService.Api.Extensions;

public static class FileMetadataMappingExtensions
{
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
