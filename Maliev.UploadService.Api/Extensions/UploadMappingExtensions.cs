using Maliev.UploadService.Data.Entities;
using Maliev.UploadService.Api.Models.Responses;

namespace Maliev.UploadService.Api.Extensions;

public static class UploadMappingExtensions
{
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

