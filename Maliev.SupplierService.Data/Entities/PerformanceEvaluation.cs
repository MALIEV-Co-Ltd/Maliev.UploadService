using Maliev.SupplierService.Data.Enums;

namespace Maliev.SupplierService.Data.Entities;

public class PerformanceEvaluation
{
    public Guid Id { get; set; }
    public Guid SupplierId { get; set; }
    public PerformanceRatingCategory RatingCategory { get; set; }
    public int Score { get; set; } // 1-5
    public DateOnly EvaluationDate { get; set; }
    public required string EvaluatorId { get; set; }
    public required string EvaluatorName { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigation property
    public Supplier Supplier { get; set; } = null!;
}
