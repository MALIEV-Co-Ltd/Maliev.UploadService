using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Maliev.SupplierService.Api.DTOs.Requests;
using Maliev.SupplierService.Api.DTOs.Responses;
using Maliev.SupplierService.Data.Enums;
using Maliev.SupplierService.Tests.Integration.Infrastructure;

namespace Maliev.SupplierService.Tests.Integration;

public class SuppliersControllerTests : BaseIntegrationTest
{
    public SuppliersControllerTests(IntegrationTestWebAppFactory factory)
        : base(factory)
    {
    }

    [Fact(Skip = "Known EF Core concurrency issue - needs investigation")]
    public async Task CreateSupplier_WithValidData_Returns201AndSupplier()
    {
        // Arrange
        var request = new CreateSupplierRequest(
            CompanyName: "Acme Corporation",
            TaxId: "ACME123456",
            Address: "123 Business Ave",
            City: "New York",
            Country: "USA",
            PostalCode: "10001",
            MaterialCategoryIds: null,
            Capabilities: ["Manufacturing", "Assembly"],
            PrimaryContact: new CreateContactRequest(
                Name: "John Doe",
                Email: "john@acme.com",
                Role: "Procurement Manager",
                Phone: "+1234567890"
            )
        );

        // Act
        var response = await Client.PostAsJsonAsync("/suppliers/v1/suppliers", request);

        // Debug
        if (response.StatusCode != HttpStatusCode.Created)
        {
            var content = await response.Content.ReadAsStringAsync();
            throw new Xunit.Sdk.XunitException($"Expected 201, got {response.StatusCode}: {content}");
        }

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var supplier = await GetResponseAsync<SupplierResponse>(response);
        supplier.Should().NotBeNull();
        supplier!.CompanyName.Should().Be("Acme Corporation");
        supplier.TaxId.Should().Be("ACME123456");
        supplier.Status.Should().Be(SupplierStatus.PendingApproval);
        supplier.Id.Should().NotBeEmpty();
    }

    [Fact]
    public async Task CreateSupplier_WithDuplicateTaxId_Returns400BadRequest()
    {
        // Arrange
        var existingSupplier = await CreateTestSupplierAsync(taxId: "DUPLICATE123");

        var request = new CreateSupplierRequest(
            CompanyName: "Another Company",
            TaxId: "DUPLICATE123",
            Address: "456 Other Street",
            City: "Chicago",
            Country: "USA",
            PostalCode: "60601",
            MaterialCategoryIds: null,
            Capabilities: null,
            PrimaryContact: null
        );

        // Act
        var response = await Client.PostAsJsonAsync("/suppliers/v1/suppliers", request);

        // Assert - InvalidOperationException maps to BadRequest in the middleware
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateSupplier_WithInvalidData_Returns400BadRequest()
    {
        // Arrange
        var request = new CreateSupplierRequest(
            CompanyName: "", // Required field empty
            TaxId: "",
            Address: "",
            City: "",
            Country: "",
            PostalCode: null,
            MaterialCategoryIds: null,
            Capabilities: null,
            PrimaryContact: null
        );

        // Act
        var response = await Client.PostAsJsonAsync("/suppliers/v1/suppliers", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetSupplier_ExistingId_Returns200WithDetails()
    {
        // Arrange
        var supplier = await CreateTestSupplierAsync(companyName: "Get Test Company");

        // Act
        var response = await Client.GetAsync($"/suppliers/v1/suppliers/{supplier.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await GetResponseAsync<SupplierDetailResponse>(response);
        result.Should().NotBeNull();
        result!.Id.Should().Be(supplier.Id);
        result.CompanyName.Should().Be("Get Test Company");
    }

    [Fact]
    public async Task GetSupplier_NonExistingId_Returns404NotFound()
    {
        // Arrange
        var nonExistingId = Guid.NewGuid();

        // Act
        var response = await Client.GetAsync($"/suppliers/v1/suppliers/{nonExistingId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ListSuppliers_WithPagination_ReturnsPagedResults()
    {
        // Arrange
        await CreateTestSupplierAsync("Company A", "TAX001");
        await CreateTestSupplierAsync("Company B", "TAX002");
        await CreateTestSupplierAsync("Company C", "TAX003");

        // Act
        var response = await Client.GetAsync("/suppliers/v1/suppliers?page=1&pageSize=2");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await GetResponseAsync<SupplierListResponse>(response);
        result.Should().NotBeNull();
        result!.Items.Should().HaveCount(2);
        result.TotalCount.Should().Be(3);
        result.TotalPages.Should().Be(2);
    }

    [Fact]
    public async Task ListSuppliers_WithStatusFilter_ReturnsFilteredResults()
    {
        // Arrange
        await CreateTestSupplierAsync("Active Company", "TAX001", SupplierStatus.Active);
        await CreateTestSupplierAsync("Pending Company", "TAX002", SupplierStatus.PendingApproval);

        // Act
        var response = await Client.GetAsync("/suppliers/v1/suppliers?status=Active");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await GetResponseAsync<SupplierListResponse>(response);
        result.Should().NotBeNull();
        result!.Items.Should().HaveCount(1);
        result.Items[0].CompanyName.Should().Be("Active Company");
    }

    [Fact]
    public async Task DeleteSupplier_ExistingId_Returns204NoContent()
    {
        // Arrange
        var supplier = await CreateTestSupplierAsync();

        // Act
        var response = await Client.DeleteAsync($"/suppliers/v1/suppliers/{supplier.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify deleted
        var getResponse = await Client.GetAsync($"/suppliers/v1/suppliers/{supplier.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateSupplier_WithValidData_Returns200()
    {
        // Arrange
        var supplier = await CreateTestSupplierAsync();

        // Get current rowVersion
        var getResponse = await Client.GetAsync($"/suppliers/v1/suppliers/{supplier.Id}");
        var current = await GetResponseAsync<SupplierDetailResponse>(getResponse);

        var request = new UpdateSupplierRequest(
            CompanyName: "Updated Company Name",
            Address: null,
            City: null,
            Country: null,
            PostalCode: null,
            MaterialCategoryIds: null,
            Capabilities: null,
            RowVersion: current!.RowVersion
        );

        // Act
        var response = await Client.PutAsJsonAsync($"/suppliers/v1/suppliers/{supplier.Id}", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await GetResponseAsync<SupplierResponse>(response);
        result!.CompanyName.Should().Be("Updated Company Name");
    }

    [Fact]
    public async Task UpdateStatus_ToActive_Returns200()
    {
        // Arrange
        var supplier = await CreateTestSupplierAsync(status: SupplierStatus.PendingApproval);

        var request = new UpdateStatusRequest(
            Status: SupplierStatus.Active,
            Reason: "Approved by admin"
        );

        // Act
        var response = await Client.PatchAsJsonAsync($"/suppliers/v1/suppliers/{supplier.Id}/status", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await GetResponseAsync<SupplierResponse>(response);
        result!.Status.Should().Be(SupplierStatus.Active);
    }
}
