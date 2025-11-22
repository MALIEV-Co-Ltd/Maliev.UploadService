using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Maliev.SupplierService.Data;
using Maliev.SupplierService.Data.Entities;
using Maliev.SupplierService.Data.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Maliev.SupplierService.Tests.Integration.Infrastructure;

public abstract class BaseIntegrationTest : IClassFixture<IntegrationTestWebAppFactory>, IAsyncLifetime
{
    protected readonly IntegrationTestWebAppFactory Factory;
    protected readonly HttpClient Client;
    protected readonly JsonSerializerOptions JsonOptions;

    protected BaseIntegrationTest(IntegrationTestWebAppFactory factory)
    {
        Factory = factory;
        Client = factory.CreateClient();

        JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        JsonOptions.Converters.Add(new JsonStringEnumConverter());
    }

    public virtual Task InitializeAsync() => Task.CompletedTask;

    public virtual async Task DisposeAsync()
    {
        // Clean up database after each test
        await CleanupDatabaseAsync();
    }

    protected async Task CleanupDatabaseAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SupplierDbContext>();

        // Delete in correct order to respect FK constraints
        await dbContext.SupplierAuditLogs.ExecuteDeleteAsync();
        await dbContext.OnboardingStatuses.ExecuteDeleteAsync();
        await dbContext.PerformanceEvaluations.ExecuteDeleteAsync();
        await dbContext.SupplierCertifications.ExecuteDeleteAsync();
        await dbContext.SupplierCapabilities.ExecuteDeleteAsync();
        await dbContext.SupplierContacts.ExecuteDeleteAsync();

        // Clear many-to-many relationship
        await dbContext.Database.ExecuteSqlRawAsync(
            "DELETE FROM supplier_material_categories");

        await dbContext.Suppliers.ExecuteDeleteAsync();
    }

    protected SupplierDbContext GetDbContext()
    {
        var scope = Factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<SupplierDbContext>();
    }

    protected async Task<Supplier> CreateTestSupplierAsync(
        string companyName = "Test Company",
        string taxId = "TEST123456",
        SupplierStatus status = SupplierStatus.PendingApproval)
    {
        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SupplierDbContext>();

        var supplier = new Supplier
        {
            Id = Guid.NewGuid(),
            CompanyName = companyName,
            TaxId = taxId,
            Address = "123 Test Street",
            City = "Test City",
            Country = "Test Country",
            PostalCode = "12345",
            Status = status,
            OnboardingStage = OnboardingStage.PendingApproval
        };

        dbContext.Suppliers.Add(supplier);
        await dbContext.SaveChangesAsync();

        return supplier;
    }

    protected async Task<MaterialCategory> CreateTestCategoryAsync(string name = "Test Category")
    {
        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SupplierDbContext>();

        var category = new MaterialCategory
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = "Test category description",
            IsActive = true
        };

        dbContext.MaterialCategories.Add(category);
        await dbContext.SaveChangesAsync();

        return category;
    }

    protected async Task<T?> GetResponseAsync<T>(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(content, JsonOptions);
    }

    protected StringContent CreateJsonContent<T>(T data)
    {
        var json = JsonSerializer.Serialize(data, JsonOptions);
        return new StringContent(json, System.Text.Encoding.UTF8, "application/json");
    }
}
