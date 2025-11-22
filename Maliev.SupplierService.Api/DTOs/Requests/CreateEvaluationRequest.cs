namespace Maliev.SupplierService.Api.DTOs.Requests;

using Maliev.SupplierService.Data.Enums;

public record CreateEvaluationRequest(
    PerformanceRatingCategory Category,
    int Score,
    string? Comments,
    DateOnly EvaluationDate
);
