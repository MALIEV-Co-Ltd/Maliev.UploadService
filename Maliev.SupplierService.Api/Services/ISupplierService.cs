using Maliev.SupplierService.Data.Entities;
using Maliev.SupplierService.Data.Enums;

namespace Maliev.SupplierService.Api.Services;

public interface ISupplierService
{
    // US1 - Register
    Task<Supplier> CreateAsync(
        string companyName,
        string taxId,
        string address,
        string city,
        string country,
        string? postalCode,
        IEnumerable<Guid>? materialCategoryIds,
        IEnumerable<string>? capabilities,
        string userId,
        string userName,
        CancellationToken cancellationToken = default);

    // US1 - Add Contact
    Task<SupplierContact> AddContactAsync(
        Guid supplierId,
        string name,
        string email,
        string? role,
        string? phone,
        bool isPrimary,
        string userId,
        string userName,
        CancellationToken cancellationToken = default);

    // US2 - Retrieve
    Task<Supplier?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    // US2 - Validate (for service-to-service integration)
    Task<(bool IsValid, Supplier? Supplier)> ValidateSupplierAsync(Guid id, CancellationToken cancellationToken = default);

    // US2 - Check Eligibility (for PO integration)
    Task<(bool IsEligible, IReadOnlyList<string> Reasons)> CheckEligibilityAsync(Guid id, CancellationToken cancellationToken = default);

    // US2 - List Material Categories
    Task<IReadOnlyList<MaterialCategory>> GetMaterialCategoriesAsync(CancellationToken cancellationToken = default);

    // US3 - Update
    Task<Supplier> UpdateAsync(
        Guid id,
        string? companyName,
        string? address,
        string? city,
        string? country,
        string? postalCode,
        IEnumerable<Guid>? materialCategoryIds,
        IEnumerable<string>? capabilities,
        long rowVersion,
        string userId,
        string userName,
        CancellationToken cancellationToken = default);

    // US3 - Update Status
    Task<Supplier> UpdateStatusAsync(
        Guid id,
        SupplierStatus newStatus,
        string? reason,
        string userId,
        string userName,
        CancellationToken cancellationToken = default);

    // US3 - Update Metadata (for external service callbacks)
    Task UpdateMetadataAsync(
        Guid id,
        DateTime? lastOrderDate,
        decimal? totalOrderValue,
        CancellationToken cancellationToken = default);

    // US4 - List/Search
    Task<(IReadOnlyList<Supplier> Items, int TotalCount)> ListSuppliersAsync(
        int page,
        int pageSize,
        SupplierStatus? status,
        Guid? categoryId,
        string? capability,
        string? search,
        string sortBy,
        string sortOrder,
        CancellationToken cancellationToken = default);

    // US5 - Certifications
    Task<SupplierCertification> AddCertificationAsync(
        Guid supplierId,
        CertificationType documentType,
        string documentName,
        DateOnly issueDate,
        DateOnly? expirationDate,
        string? externalFileRef,
        string? notes,
        string userId,
        string userName,
        CancellationToken cancellationToken = default);

    Task DeleteCertificationAsync(
        Guid supplierId,
        Guid certificationId,
        string userId,
        string userName,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<(SupplierCertification Certification, Supplier Supplier, int DaysUntilExpiration)>> GetExpiringCertificationsAsync(
        int daysThreshold,
        CancellationToken cancellationToken = default);

    // US6 - Performance Evaluations
    Task<PerformanceEvaluation> AddEvaluationAsync(
        Guid supplierId,
        PerformanceRatingCategory category,
        int score,
        string? comments,
        DateOnly evaluationDate,
        string userId,
        string userName,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PerformanceEvaluation>> GetEvaluationsAsync(
        Guid supplierId,
        CancellationToken cancellationToken = default);

    // US7 - Onboarding
    Task<Supplier> AdvanceOnboardingAsync(
        Guid supplierId,
        OnboardingStage targetStage,
        string? notes,
        string userId,
        string userName,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OnboardingStatus>> GetOnboardingHistoryAsync(
        Guid supplierId,
        CancellationToken cancellationToken = default);

    // US8 - Audit Trail
    Task<(IReadOnlyList<SupplierAuditLog> Items, int TotalCount)> GetAuditTrailAsync(
        Guid supplierId,
        DateTime? startDate,
        DateTime? endDate,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    // Deletion
    Task DeleteAsync(
        Guid id,
        string userId,
        string userName,
        CancellationToken cancellationToken = default);
}
