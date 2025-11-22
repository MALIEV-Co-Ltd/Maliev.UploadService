namespace Maliev.SupplierService.Api.DTOs.Responses;

using Maliev.SupplierService.Data.Enums;

public record OnboardingStatusResponse(
    Guid Id,
    OnboardingStage Stage,
    DateTime TransitionedAt,
    string TransitionedBy,
    string TransitionedByName,
    string? Notes
);

public record OnboardingHistoryResponse(
    OnboardingStage CurrentStage,
    IReadOnlyList<OnboardingStatusResponse> History
);
