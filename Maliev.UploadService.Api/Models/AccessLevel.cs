namespace Maliev.UploadService.Api.Models;

/// <summary>
/// Access level for file authorization
/// </summary>
public enum AccessLevel
{
    /// <summary>
    /// Public access - no authentication required
    /// </summary>
    Public,

    /// <summary>
    /// Internal access - requires Guest role or higher
    /// </summary>
    Internal,

    /// <summary>
    /// Restricted access - requires Sales role or higher
    /// </summary>
    Restricted,

    /// <summary>
    /// Confidential access - requires Manager role or higher
    /// </summary>
    Confidential
}