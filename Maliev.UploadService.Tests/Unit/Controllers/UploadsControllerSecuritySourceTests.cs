namespace Maliev.UploadService.Tests.Unit.Controllers;

/// <summary>
/// Source-level regression tests for upload path authorization checks.
/// </summary>
public sealed class UploadsControllerSecuritySourceTests
{
    /// <summary>
    /// Verifies caller-chosen resumable and artifact paths are explicitly authorized before storage mutation.
    /// </summary>
    [Fact]
    public void UploadPathMutationsAuthorizeSanitizedPathBeforeStorageMutation()
    {
        var source = ReadRepoFile("Maliev.UploadService.Api", "Controllers", "v1", "UploadsController.cs");

        AssertCallPrecedes(source, "InitiateResumableUpload", "CanUploadToPathAsync(serviceName, sanitizedPath", "InitiateResumableUploadAsync(");
        AssertCallPrecedes(source, "UploadArtifact", "CanUploadToPathAsync(serviceName, sanitizedPath", "UploadFileAsync(");
    }

    private static void AssertCallPrecedes(string source, string actionName, string guardCall, string sinkCall)
    {
        var actionStart = source.IndexOf($"public async Task<IActionResult> {actionName}", StringComparison.Ordinal);
        Assert.True(actionStart >= 0, $"{actionName} action was not found.");

        var guardIndex = source.IndexOf(guardCall, actionStart, StringComparison.Ordinal);
        Assert.True(guardIndex > actionStart, $"{actionName} does not authorize the sanitized path.");

        var sinkIndex = source.IndexOf(sinkCall, actionStart, StringComparison.Ordinal);
        Assert.True(sinkIndex > actionStart, $"{actionName} storage sink was not found.");
        Assert.True(guardIndex < sinkIndex, $"{actionName} mutates storage before authorizing the sanitized path.");
    }

    private static string ReadRepoFile(params string[] pathSegments)
    {
        var root = FindRepoRoot();
        return File.ReadAllText(Path.Combine([root, .. pathSegments]));
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Maliev.UploadService.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate Maliev.UploadService repository root.");
    }
}
