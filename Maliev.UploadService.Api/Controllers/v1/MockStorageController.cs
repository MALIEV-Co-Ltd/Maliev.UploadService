using Asp.Versioning;
using Maliev.UploadService.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;

namespace Maliev.UploadService.Api.Controllers.v1;

/// <summary>
/// Serves local mock storage objects through signed mock URLs.
/// </summary>
[ApiController]
[ApiVersion("1")]
[Route("upload/v{version:apiVersion}/mock-storage")]
public sealed class MockStorageController : ControllerBase
{
    /// <summary>
    /// Downloads a locally stored mock object by signed token.
    /// </summary>
    /// <param name="token">The opaque signed URL token.</param>
    /// <returns>The stored object content when the token is valid.</returns>
    [HttpGet("{token}")]
    [HttpHead("{token}")]
    [AllowAnonymous]
    [EnableCors(Program.MockStorageCorsPolicy)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult Download(string token)
    {
        if (!MockStorageService.TryGetSignedObject(token, out var content, out var contentType, out _))
        {
            return NotFound();
        }

        Response.ContentLength = content.LongLength;
        Response.ContentType = contentType;

        if (HttpMethods.IsHead(Request.Method))
        {
            return new EmptyResult();
        }

        return File(content, contentType, enableRangeProcessing: true);
    }
}
