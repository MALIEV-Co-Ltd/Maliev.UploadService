namespace Maliev.SupplierService.Api.DTOs.Responses;

using Maliev.SupplierService.Data.Enums;

public record EvaluationResponse(
    Guid Id,
    PerformanceRatingCategory Category,
    int Score,
    string? Comments,
    DateOnly EvaluationDate,
    string EvaluatedBy,
    string EvaluatedByName,
    DateTime CreatedAt
);

public record EvaluationListResponse(
    IReadOnlyList<EvaluationResponse> Items,
    int TotalCount,
    decimal? AverageScore
);
