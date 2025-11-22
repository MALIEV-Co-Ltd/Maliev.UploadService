namespace Maliev.SupplierService.Api.DTOs.Responses;

using Maliev.SupplierService.Data.Enums;

public record SupplierDetailResponse(
    Guid Id,
    string CompanyName,
    string TaxId,
    string Address,
    string City,
    string Country,
    string? PostalCode,
    SupplierStatus Status,
    OnboardingStage OnboardingStage,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    string RowVersion,
    IReadOnlyList<ContactResponse> Contacts,
    IReadOnlyList<MaterialCategoryResponse> MaterialCategories,
    IReadOnlyList<CapabilityResponse> Capabilities,
    IReadOnlyList<CertificationResponse> Certifications,
    PerformanceSummaryResponse? PerformanceSummary
);

public record PerformanceSummaryResponse(
    decimal? OverallRating,
    decimal? QualityRating,
    decimal? DeliveryRating,
    decimal? CommunicationRating,
    decimal? PricingRating,
    int TotalEvaluations,
    DateTime? LastEvaluationDate
);
