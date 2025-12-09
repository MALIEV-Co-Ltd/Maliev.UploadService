using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Maliev.UploadService.Api.Data;
using Maliev.UploadService.Api.Models.Entities;
using Maliev.UploadService.Api.Models.Responses;
using Maliev.UploadService.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Maliev.UploadService.Tests.Integration;

/// <summary>
/// T126-T128: Integration tests for lifecycle management and retention policies
/// </summary>
public class LifecycleManagementTests : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
{
    private readonly TestWebApplicationFactory _factory;
    private HttpClient _client = null!;
    private string _authToken = null!;
    private string _retentionPolicyId = null!;

    public LifecycleManagementTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        _client = _factory.CreateClient();
        _authToken = GenerateJwtToken("test-service", "uploadservice");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _authToken);

        // Create a test retention policy
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<UploadServiceDbContext>();

        var retentionPolicy = new RetentionPolicy
        {
            PolicyId = Guid.NewGuid().ToString(),
            PolicyName = "7-day-retention",
            ServiceId = "test-service",
            RetentionDays = 7,
            StorageClassTransitions = new List<StorageClassTransition>
            {
                new StorageClassTransition { Days = 3, StorageClass = "NEARLINE" },
                new StorageClassTransition { Days = 5, StorageClass = "COLDLINE" }
            },
            ApplyToPathPrefix = "test-service/",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        dbContext.RetentionPolicies.Add(retentionPolicy);
        await dbContext.SaveChangesAsync();
        _retentionPolicyId = retentionPolicy.PolicyId;
    }

    public Task DisposeAsync()
    {
        _client?.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>
    /// T126: Test that retention policy is applied to uploaded files
    /// </summary>
    [Fact]
    public async Task UploadFile_WithRetentionPolicy_AppliesPolicyToFile()
    {
        // Arrange
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("Test file with retention"));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        content.Add(fileContent, "File", "retention-test.txt");
        content.Add(new StringContent("test-service/lifecycle/retention-test.txt"), "Path");
        content.Add(new StringContent("test-service"), "ServiceName");
        content.Add(new StringContent(_retentionPolicyId), "RetentionPolicyId");

        // Act
        var response = await _client.PostAsync("/api/v1/uploads", content);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<UploadResponse>();
        Assert.NotNull(result);

        // Verify file metadata has retention policy applied
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<UploadServiceDbContext>();
        var fileMetadata = await dbContext.FileMetadata
            .FirstOrDefaultAsync(f => f.UploadId == result.UploadId);

        Assert.NotNull(fileMetadata);
        Assert.Equal(_retentionPolicyId, fileMetadata.RetentionPolicyId);
        Assert.NotNull(fileMetadata.ExpiresAt);
        Assert.True(fileMetadata.ExpiresAt > DateTime.UtcNow);
        Assert.True(fileMetadata.ExpiresAt <= DateTime.UtcNow.AddDays(7).AddHours(1)); // Allow 1hr buffer
    }

    /// <summary>
    /// T127: Test indefinite retention (RetentionDays = 0)
    /// </summary>
    [Fact]
    public async Task UploadFile_WithIndefiniteRetention_NoExpirationDate()
    {
        // Arrange - Create indefinite retention policy
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<UploadServiceDbContext>();

        var indefinitePolicy = new RetentionPolicy
        {
            PolicyId = Guid.NewGuid().ToString(),
            PolicyName = "indefinite-retention",
            ServiceId = "test-service",
            RetentionDays = 0, // Indefinite
            ApplyToPathPrefix = "test-service/",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        dbContext.RetentionPolicies.Add(indefinitePolicy);
        await dbContext.SaveChangesAsync();

        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("Test file with indefinite retention"));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        content.Add(fileContent, "File", "indefinite-test.txt");
        content.Add(new StringContent("test-service/lifecycle/indefinite-test.txt"), "Path");
        content.Add(new StringContent("test-service"), "ServiceName");
        content.Add(new StringContent(indefinitePolicy.PolicyId), "RetentionPolicyId");

        // Act
        var response = await _client.PostAsync("/api/v1/uploads", content);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<UploadResponse>();
        Assert.NotNull(result);

        // Verify file metadata has no expiration date
        var fileMetadata = await dbContext.FileMetadata
            .FirstOrDefaultAsync(f => f.UploadId == result.UploadId);

        Assert.NotNull(fileMetadata);
        Assert.Equal(indefinitePolicy.PolicyId, fileMetadata.RetentionPolicyId);
        Assert.Null(fileMetadata.ExpiresAt); // Should be null for indefinite retention
    }

    /// <summary>
    /// T128: Test storage class transitions are set correctly
    /// </summary>
    [Fact]
    public async Task UploadFile_WithStorageClassTransitions_SetsTransitionMetadata()
    {
        // Arrange
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("Test file with transitions"));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        content.Add(fileContent, "File", "transition-test.txt");
        content.Add(new StringContent("test-service/lifecycle/transition-test.txt"), "Path");
        content.Add(new StringContent("test-service"), "ServiceName");
        content.Add(new StringContent(_retentionPolicyId), "RetentionPolicyId");

        // Act
        var response = await _client.PostAsync("/api/v1/uploads", content);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<UploadResponse>();
        Assert.NotNull(result);

        // Verify retention policy has transitions
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<UploadServiceDbContext>();
        var policy = await dbContext.RetentionPolicies.FindAsync(_retentionPolicyId);

        Assert.NotNull(policy);
        Assert.NotNull(policy.StorageClassTransitions);
        Assert.Equal(2, policy.StorageClassTransitions.Count);
        Assert.Equal(3, policy.StorageClassTransitions[0].Days);
        Assert.Equal("NEARLINE", policy.StorageClassTransitions[0].StorageClass);
        Assert.Equal(5, policy.StorageClassTransitions[1].Days);
        Assert.Equal("COLDLINE", policy.StorageClassTransitions[1].StorageClass);
    }

    private string GenerateJwtToken(string serviceName, string audience)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, serviceName),
            new Claim(JwtRegisteredClaimNames.Sub, serviceName),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim("service_id", serviceName)
        };

        var token = new JwtSecurityToken(
            issuer: "https://test.maliev.com",
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: _factory.SigningCredentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
