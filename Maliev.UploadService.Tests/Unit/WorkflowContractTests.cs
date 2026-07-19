namespace Maliev.UploadService.Tests.Unit;

/// <summary>
/// Protects read-only, credential-free validation for every repository trigger.
/// </summary>
public sealed class WorkflowContractTests
{
    private static readonly string Root = FindRoot();
    private static readonly string Workflows = Path.Combine(Root, ".github", "workflows");

    /// <summary>
    /// PR, branch, and release-tag triggers must all call the same read-only validator.
    /// </summary>
    [Theory]
    [InlineData("pr-validation.yml", "pull_request:")]
    [InlineData("ci-main.yml", "main")]
    [InlineData("ci-develop.yml", "develop")]
    [InlineData("ci-staging.yml", "release/v*")]
    public void TriggerWorkflow_UsesReadOnlyReusableValidation(string file, string trigger)
    {
        var source = ReadWorkflow(file);

        Assert.Contains(trigger, source, StringComparison.Ordinal);
        Assert.Contains("contents: read", source, StringComparison.Ordinal);
        Assert.Contains("uses: ./.github/workflows/_validate.yml", source, StringComparison.Ordinal);
        AssertSafe(source);
    }

    /// <summary>
    /// Validation reconstructs exact shared projects without private package credentials.
    /// </summary>
    [Fact]
    public void ReusableValidation_UsesImmutablePublicSources()
    {
        var source = ReadWorkflow("_validate.yml");

        Assert.Contains("actions/checkout@9c091bb21b7c1c1d1991bb908d89e4e9dddfe3e0", source, StringComparison.Ordinal);
        Assert.Contains("actions/setup-dotnet@a98b56852c35b8e3190ac28c8c2271da59106c68", source, StringComparison.Ordinal);
        Assert.Contains("ref: 20a2746e024de6bd070b51af99bab7b761612c06", source, StringComparison.Ordinal);
        Assert.Contains("ref: 9c41d6524a485bf03ba022b8170f47366ab1a77a", source, StringComparison.Ordinal);
        Assert.Contains("GITHUB_ACTIONS=false dotnet restore", source, StringComparison.Ordinal);
        Assert.Contains("--configfile nuget.validation.config", source, StringComparison.Ordinal);

        var nuget = File.ReadAllText(Path.Combine(Root, "nuget.validation.config"));
        Assert.Contains("<clear />", nuget, StringComparison.Ordinal);
        Assert.DoesNotContain("nuget.pkg.github.com", nuget, StringComparison.OrdinalIgnoreCase);
        AssertSafe(source);
    }

    /// <summary>
    /// No workflow may receive credentials or mutate deployment state.
    /// </summary>
    [Fact]
    public void EveryWorkflow_ForbidsCredentialsAndDeploymentMutation()
    {
        foreach (var file in Directory.GetFiles(Workflows, "*.yml"))
        {
            AssertSafe(File.ReadAllText(file));
        }
    }

    /// <summary>
    /// Direct Microsoft.Extensions dependencies must not undercut the exact shared defaults graph.
    /// </summary>
    [Fact]
    public void ApplicationPackageFloors_MatchSharedDefaults()
    {
        var project = File.ReadAllText(Path.Combine(
            Root,
            "Maliev.UploadService.Application",
            "Maliev.UploadService.Application.csproj"));

        Assert.Contains(
            "<PackageReference Include=\"Microsoft.Extensions.Configuration.Abstractions\" Version=\"10.0.10\" />",
            project,
            StringComparison.Ordinal);
        Assert.Contains(
            "<PackageReference Include=\"Microsoft.Extensions.Logging.Abstractions\" Version=\"10.0.10\" />",
            project,
            StringComparison.Ordinal);
    }

    private static void AssertSafe(string source)
    {
        foreach (var forbidden in new[]
                 {
                     "secrets.", "GITOPS_PAT", "GCP_SA_KEY", "NUGET_PASSWORD",
                     "id-token: write", "credentials_json", "google-github-actions/auth", "gcloud auth",
                     "docker push", "maliev-gitops", "kustomize edit", "git push origin", "gh pr create",
                     "pull_request_target"
                 })
        {
            Assert.DoesNotContain(forbidden, source, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static string ReadWorkflow(string file)
    {
        var path = Path.Combine(Workflows, file);
        Assert.True(File.Exists(path), $"Required workflow is missing: {file}");
        return File.ReadAllText(path);
    }

    private static string FindRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Maliev.UploadService.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate UploadService repository root.");
    }
}
