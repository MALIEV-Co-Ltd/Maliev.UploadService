using Maliev.Aspire.ServiceDefaults.Authorization;
using Maliev.UploadService.Api.Controllers.v1;
using Microsoft.AspNetCore.Authorization;

namespace Maliev.UploadService.Tests.Unit.Controllers;

public sealed class ScopedEndpointAuthorizationMetadataTests
{
    public static TheoryData<Type, string> ScopedActions => new()
    {
        { typeof(UploadsController), nameof(UploadsController.UploadFile) },
        { typeof(UploadsController), nameof(UploadsController.UploadStream) },
        { typeof(UploadsController), nameof(UploadsController.InitiateResumableUpload) },
        { typeof(UploadsController), nameof(UploadsController.CompleteResumableUpload) },
        { typeof(UploadsController), nameof(UploadsController.ResumeUpload) },
        { typeof(UploadsController), nameof(UploadsController.UploadArtifact) },
        { typeof(FilesController), nameof(FilesController.GetFileMetadata) },
        { typeof(FilesController), nameof(FilesController.GenerateSignedUrl) },
        { typeof(FilesController), nameof(FilesController.GenerateSignedUrlByPath) },
        { typeof(FilesController), nameof(FilesController.DeleteFile) }
    };

    [Theory]
    [MemberData(nameof(ScopedActions))]
    public void ScopedAction_UsesSubjectAuthenticationPolicyWithoutFlatPermissionGate(
        Type controllerType,
        string actionName)
    {
        var action = controllerType.GetMethod(actionName);
        Assert.NotNull(action);

        var authorize = Assert.Single(action.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true));
        Assert.Equal("upload.authenticated-subject", ((AuthorizeAttribute)authorize).Policy);
        Assert.Empty(action.GetCustomAttributes(typeof(RequirePermissionAttribute), inherit: true));
    }
}
