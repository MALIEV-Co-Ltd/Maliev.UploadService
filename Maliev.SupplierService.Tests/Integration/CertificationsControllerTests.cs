using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Maliev.SupplierService.Api.DTOs.Requests;
using Maliev.SupplierService.Api.DTOs.Responses;
using Maliev.SupplierService.Data.Enums;
using Maliev.SupplierService.Tests.Integration.Infrastructure;

namespace Maliev.SupplierService.Tests.Integration;

public class CertificationsControllerTests : BaseIntegrationTest
{
    public CertificationsControllerTests(IntegrationTestWebAppFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task AddCertification_WithValidData_Returns201()
    {
        // Arrange
        var supplier = await CreateTestSupplierAsync();

        var request = new CreateCertificationRequest(
            DocumentType: CertificationType.BusinessLicense,
            DocumentName: "Business License 2024",
            IssueDate: DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-6)),
            ExpirationDate: DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(6)),
            ExternalFileRef: "https://storage.example.com/docs/license.pdf",
            Notes: "Annual business license"
        );

        // Act
        var response = await Client.PostAsJsonAsync(
            $"/suppliers/v1/suppliers/{supplier.Id}/certifications", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var result = await GetResponseAsync<CertificationResponse>(response);
        result.Should().NotBeNull();
        result!.DocumentName.Should().Be("Business License 2024");
        result.IsExpired.Should().BeFalse();
        result.IsExpiringSoon.Should().BeFalse();
    }

    [Fact]
    public async Task AddCertification_WithExpiredDate_ShowsExpired()
    {
        // Arrange
        var supplier = await CreateTestSupplierAsync();

        var request = new CreateCertificationRequest(
            DocumentType: CertificationType.TaxForm,
            DocumentName: "Expired Tax Form",
            IssueDate: DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-2)),
            ExpirationDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
            ExternalFileRef: null,
            Notes: null
        );

        // Act
        var response = await Client.PostAsJsonAsync(
            $"/suppliers/v1/suppliers/{supplier.Id}/certifications", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var result = await GetResponseAsync<CertificationResponse>(response);
        result!.IsExpired.Should().BeTrue();
    }

    [Fact]
    public async Task AddCertification_NonExistingSupplier_Returns404()
    {
        // Arrange
        var request = new CreateCertificationRequest(
            DocumentType: CertificationType.BusinessLicense,
            DocumentName: "Test",
            IssueDate: DateOnly.FromDateTime(DateTime.UtcNow),
            ExpirationDate: null,
            ExternalFileRef: null,
            Notes: null
        );

        // Act
        var response = await Client.PostAsJsonAsync(
            $"/suppliers/v1/suppliers/{Guid.NewGuid()}/certifications", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetExpiringCertifications_ReturnsCertificationsWithinThreshold()
    {
        // Arrange
        var supplier = await CreateTestSupplierAsync();

        // Add certification expiring in 15 days
        var expiringRequest = new CreateCertificationRequest(
            DocumentType: CertificationType.InsuranceCertificate,
            DocumentName: "Expiring Insurance",
            IssueDate: DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-11)),
            ExpirationDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(15)),
            ExternalFileRef: null,
            Notes: null
        );
        await Client.PostAsJsonAsync(
            $"/suppliers/v1/suppliers/{supplier.Id}/certifications", expiringRequest);

        // Add certification expiring in 60 days (outside 30-day threshold)
        var notExpiringRequest = new CreateCertificationRequest(
            DocumentType: CertificationType.QualityCertification,
            DocumentName: "Valid Certification",
            IssueDate: DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-6)),
            ExpirationDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(60)),
            ExternalFileRef: null,
            Notes: null
        );
        await Client.PostAsJsonAsync(
            $"/suppliers/v1/suppliers/{supplier.Id}/certifications", notExpiringRequest);

        // Act
        var response = await Client.GetAsync("/suppliers/v1/certifications/expiring?days=30");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await GetResponseAsync<ExpiringCertificationsListResponse>(response);
        result.Should().NotBeNull();
        result!.Items.Should().HaveCount(1);
        result.Items[0].DocumentName.Should().Be("Expiring Insurance");
        result.Items[0].DaysUntilExpiration.Should().BeLessThanOrEqualTo(30);
    }

    [Fact]
    public async Task DeleteCertification_ExistingId_Returns204()
    {
        // Arrange
        var supplier = await CreateTestSupplierAsync();

        var createRequest = new CreateCertificationRequest(
            DocumentType: CertificationType.TaxForm,
            DocumentName: "To Delete",
            IssueDate: DateOnly.FromDateTime(DateTime.UtcNow),
            ExpirationDate: null,
            ExternalFileRef: null,
            Notes: null
        );

        var createResponse = await Client.PostAsJsonAsync(
            $"/suppliers/v1/suppliers/{supplier.Id}/certifications", createRequest);
        var certification = await GetResponseAsync<CertificationResponse>(createResponse);

        // Act
        var response = await Client.DeleteAsync(
            $"/suppliers/v1/suppliers/{supplier.Id}/certifications/{certification!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
