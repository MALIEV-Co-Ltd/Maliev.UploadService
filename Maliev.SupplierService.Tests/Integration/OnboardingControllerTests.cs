using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Maliev.SupplierService.Api.DTOs.Requests;
using Maliev.SupplierService.Api.DTOs.Responses;
using Maliev.SupplierService.Data.Enums;
using Maliev.SupplierService.Tests.Integration.Infrastructure;

namespace Maliev.SupplierService.Tests.Integration;

public class OnboardingControllerTests : BaseIntegrationTest
{
    public OnboardingControllerTests(IntegrationTestWebAppFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task AdvanceOnboarding_ValidTransition_Returns200()
    {
        // Arrange
        var supplier = await CreateTestSupplierAsync(); // Starts at PendingApproval

        var request = new AdvanceOnboardingRequest(
            TargetStage: OnboardingStage.DocumentationReview,
            Notes: "Documents received, starting review"
        );

        // Act
        var response = await Client.PostAsJsonAsync(
            $"/suppliers/v1/suppliers/{supplier.Id}/onboarding", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await GetResponseAsync<SupplierResponse>(response);
        result!.OnboardingStage.Should().Be(OnboardingStage.DocumentationReview);
    }

    [Fact]
    public async Task AdvanceOnboarding_InvalidTransition_Returns400()
    {
        // Arrange
        var supplier = await CreateTestSupplierAsync(); // Starts at PendingApproval

        // Try to skip directly to Active (invalid)
        var request = new AdvanceOnboardingRequest(
            TargetStage: OnboardingStage.Active,
            Notes: "Trying to skip stages"
        );

        // Act
        var response = await Client.PostAsJsonAsync(
            $"/suppliers/v1/suppliers/{supplier.Id}/onboarding", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AdvanceOnboarding_ToActive_SetsSupplierStatusActive()
    {
        // Arrange
        var supplier = await CreateTestSupplierAsync();

        // Progress through all stages
        await Client.PostAsJsonAsync(
            $"/suppliers/v1/suppliers/{supplier.Id}/onboarding",
            new AdvanceOnboardingRequest(OnboardingStage.DocumentationReview, "Step 1"));

        await Client.PostAsJsonAsync(
            $"/suppliers/v1/suppliers/{supplier.Id}/onboarding",
            new AdvanceOnboardingRequest(OnboardingStage.FinalApproval, "Step 2"));

        // Act - advance to Active
        var response = await Client.PostAsJsonAsync(
            $"/suppliers/v1/suppliers/{supplier.Id}/onboarding",
            new AdvanceOnboardingRequest(OnboardingStage.Active, "Approved!"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await GetResponseAsync<SupplierResponse>(response);
        result!.OnboardingStage.Should().Be(OnboardingStage.Active);
        result.Status.Should().Be(SupplierStatus.Active);
    }

    [Fact]
    public async Task GetOnboardingHistory_ReturnsAllTransitions()
    {
        // Arrange
        var supplier = await CreateTestSupplierAsync();

        // Make some transitions
        await Client.PostAsJsonAsync(
            $"/suppliers/v1/suppliers/{supplier.Id}/onboarding",
            new AdvanceOnboardingRequest(OnboardingStage.DocumentationReview, "First transition"));

        await Client.PostAsJsonAsync(
            $"/suppliers/v1/suppliers/{supplier.Id}/onboarding",
            new AdvanceOnboardingRequest(OnboardingStage.FinalApproval, "Second transition"));

        // Act
        var response = await Client.GetAsync(
            $"/suppliers/v1/suppliers/{supplier.Id}/onboarding");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await GetResponseAsync<OnboardingHistoryResponse>(response);
        result.Should().NotBeNull();
        result!.CurrentStage.Should().Be(OnboardingStage.FinalApproval);
        result.History.Should().HaveCountGreaterOrEqualTo(2);
    }

    [Fact]
    public async Task AdvanceOnboarding_NonExistingSupplier_Returns404()
    {
        // Arrange
        var request = new AdvanceOnboardingRequest(
            TargetStage: OnboardingStage.DocumentationReview,
            Notes: null
        );

        // Act
        var response = await Client.PostAsJsonAsync(
            $"/suppliers/v1/suppliers/{Guid.NewGuid()}/onboarding", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
