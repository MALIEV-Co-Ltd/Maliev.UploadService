using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Maliev.SupplierService.Api.DTOs.Requests;
using Maliev.SupplierService.Api.DTOs.Responses;
using Maliev.SupplierService.Data.Enums;
using Maliev.SupplierService.Tests.Integration.Infrastructure;

namespace Maliev.SupplierService.Tests.Integration;

public class EvaluationsControllerTests : BaseIntegrationTest
{
    public EvaluationsControllerTests(IntegrationTestWebAppFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task AddEvaluation_WithValidData_Returns201()
    {
        // Arrange
        var supplier = await CreateTestSupplierAsync();

        var request = new CreateEvaluationRequest(
            Category: PerformanceRatingCategory.Quality,
            Score: 4,
            Comments: "Good quality products",
            EvaluationDate: DateOnly.FromDateTime(DateTime.UtcNow)
        );

        // Act
        var response = await Client.PostAsJsonAsync(
            $"/suppliers/v1/suppliers/{supplier.Id}/evaluations", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var result = await GetResponseAsync<EvaluationResponse>(response);
        result.Should().NotBeNull();
        result!.Score.Should().Be(4);
        result.Category.Should().Be(PerformanceRatingCategory.Quality);
    }

    [Fact]
    public async Task AddEvaluation_WithInvalidScore_Returns400()
    {
        // Arrange
        var supplier = await CreateTestSupplierAsync();

        var request = new CreateEvaluationRequest(
            Category: PerformanceRatingCategory.Quality,
            Score: 6, // Invalid - should be 1-5
            Comments: null,
            EvaluationDate: DateOnly.FromDateTime(DateTime.UtcNow)
        );

        // Act
        var response = await Client.PostAsJsonAsync(
            $"/suppliers/v1/suppliers/{supplier.Id}/evaluations", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AddEvaluation_WithFutureDate_Returns400()
    {
        // Arrange
        var supplier = await CreateTestSupplierAsync();

        var request = new CreateEvaluationRequest(
            Category: PerformanceRatingCategory.Delivery,
            Score: 3,
            Comments: null,
            EvaluationDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7))
        );

        // Act
        var response = await Client.PostAsJsonAsync(
            $"/suppliers/v1/suppliers/{supplier.Id}/evaluations", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetEvaluations_ReturnsListWithAverage()
    {
        // Arrange
        var supplier = await CreateTestSupplierAsync();

        // Add multiple evaluations
        await Client.PostAsJsonAsync(
            $"/suppliers/v1/suppliers/{supplier.Id}/evaluations",
            new CreateEvaluationRequest(PerformanceRatingCategory.Quality, 5, null, DateOnly.FromDateTime(DateTime.UtcNow)));

        await Client.PostAsJsonAsync(
            $"/suppliers/v1/suppliers/{supplier.Id}/evaluations",
            new CreateEvaluationRequest(PerformanceRatingCategory.Delivery, 3, null, DateOnly.FromDateTime(DateTime.UtcNow)));

        await Client.PostAsJsonAsync(
            $"/suppliers/v1/suppliers/{supplier.Id}/evaluations",
            new CreateEvaluationRequest(PerformanceRatingCategory.Communication, 4, null, DateOnly.FromDateTime(DateTime.UtcNow)));

        // Act
        var response = await Client.GetAsync(
            $"/suppliers/v1/suppliers/{supplier.Id}/evaluations");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await GetResponseAsync<EvaluationListResponse>(response);
        result.Should().NotBeNull();
        result!.Items.Should().HaveCount(3);
        result.TotalCount.Should().Be(3);
        result.AverageScore.Should().Be(4m); // (5 + 3 + 4) / 3 = 4
    }

    [Fact]
    public async Task AddEvaluation_NonExistingSupplier_Returns404()
    {
        // Arrange
        var request = new CreateEvaluationRequest(
            Category: PerformanceRatingCategory.Pricing,
            Score: 3,
            Comments: null,
            EvaluationDate: DateOnly.FromDateTime(DateTime.UtcNow)
        );

        // Act
        var response = await Client.PostAsJsonAsync(
            $"/suppliers/v1/suppliers/{Guid.NewGuid()}/evaluations", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public async Task AddEvaluation_ValidScores_Accepted(int score)
    {
        // Arrange
        var supplier = await CreateTestSupplierAsync(taxId: $"TAX{score}");

        var request = new CreateEvaluationRequest(
            Category: PerformanceRatingCategory.Overall,
            Score: score,
            Comments: $"Score {score} test",
            EvaluationDate: DateOnly.FromDateTime(DateTime.UtcNow)
        );

        // Act
        var response = await Client.PostAsJsonAsync(
            $"/suppliers/v1/suppliers/{supplier.Id}/evaluations", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }
}
