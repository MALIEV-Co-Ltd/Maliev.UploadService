namespace Maliev.SupplierService.Api.DTOs.Requests;

using Maliev.SupplierService.Data.Enums;

public record AdvanceOnboardingRequest(
    OnboardingStage TargetStage,
    string? Notes
);
