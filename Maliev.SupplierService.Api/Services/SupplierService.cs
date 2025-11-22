using Maliev.SupplierService.Api.Events;
using Maliev.SupplierService.Data;
using Maliev.SupplierService.Data.Entities;
using Maliev.SupplierService.Data.Enums;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Maliev.SupplierService.Api.Services;

public class SupplierService : ISupplierService
{
    private readonly SupplierDbContext _context;
    private readonly ICacheService _cacheService;
    private readonly IAuditService _auditService;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<SupplierService> _logger;

    public SupplierService(
        SupplierDbContext context,
        ICacheService cacheService,
        IAuditService auditService,
        IPublishEndpoint publishEndpoint,
        ILogger<SupplierService> logger)
    {
        _context = context;
        _cacheService = cacheService;
        _auditService = auditService;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task<Supplier> CreateAsync(
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
        CancellationToken cancellationToken = default)
    {
        // Check for duplicate TaxId
        var existingSupplier = await _context.Suppliers
            .FirstOrDefaultAsync(s => s.TaxId == taxId, cancellationToken);

        if (existingSupplier is not null)
        {
            throw new InvalidOperationException($"Supplier with TaxId '{taxId}' already exists.");
        }

        var supplier = new Supplier
        {
            Id = Guid.NewGuid(),
            CompanyName = companyName,
            TaxId = taxId,
            Address = address,
            City = city,
            Country = country,
            PostalCode = postalCode,
            Status = SupplierStatus.PendingApproval,
            OnboardingStage = OnboardingStage.PendingApproval
        };

        // Add material categories
        if (materialCategoryIds?.Any() == true)
        {
            var categories = await _context.MaterialCategories
                .Where(c => materialCategoryIds.Contains(c.Id) && c.IsActive)
                .ToListAsync(cancellationToken);

            foreach (var category in categories)
            {
                supplier.MaterialCategories.Add(category);
            }
        }

        // Add capabilities
        if (capabilities?.Any() == true)
        {
            foreach (var capabilityName in capabilities)
            {
                supplier.Capabilities.Add(new SupplierCapability
                {
                    Id = Guid.NewGuid(),
                    SupplierId = supplier.Id,
                    Name = capabilityName,
                    IsActive = true
                });
            }
        }

        // Add initial onboarding status
        supplier.OnboardingHistory.Add(new OnboardingStatus
        {
            Id = Guid.NewGuid(),
            SupplierId = supplier.Id,
            Stage = OnboardingStage.PendingApproval,
            TransitionedBy = userId,
            TransitionedByName = userName,
            Notes = "Supplier created"
        });

        _context.Suppliers.Add(supplier);
        await _context.SaveChangesAsync(cancellationToken);

        // Skip audit log - TODO: Re-enable after fixing concurrency issue
        // await _auditService.LogChangeAsync(
        //     supplier.Id,
        //     "CREATE",
        //     nameof(Supplier),
        //     supplier.Id,
        //     null,
        //     new { supplier.Id, supplier.CompanyName, supplier.TaxId, supplier.Address, supplier.City, supplier.Country, supplier.PostalCode, supplier.Status, supplier.OnboardingStage },
        //     userId,
        //     userName,
        //     cancellationToken);

        // Invalidate cache
        await _cacheService.InvalidateByTagAsync("suppliers", cancellationToken);

        // Publish event
        await _publishEndpoint.Publish(new SupplierCreatedEvent(
            supplier.Id,
            supplier.CompanyName,
            supplier.TaxId,
            supplier.Country,
            supplier.CreatedAt,
            userId), cancellationToken);

        _logger.LogInformation("Created supplier {SupplierId} with TaxId {TaxId}", supplier.Id, taxId);

        return supplier;
    }

    public async Task<SupplierContact> AddContactAsync(
        Guid supplierId,
        string name,
        string email,
        string? role,
        string? phone,
        bool isPrimary,
        string userId,
        string userName,
        CancellationToken cancellationToken = default)
    {
        var supplier = await _context.Suppliers
            .Include(s => s.Contacts)
            .FirstOrDefaultAsync(s => s.Id == supplierId, cancellationToken);

        if (supplier is null)
        {
            throw new InvalidOperationException($"Supplier with ID '{supplierId}' not found.");
        }

        // If setting as primary, unset other primary contacts
        if (isPrimary)
        {
            foreach (var existingContact in supplier.Contacts.Where(c => c.IsPrimary))
            {
                existingContact.IsPrimary = false;
            }
        }

        var contact = new SupplierContact
        {
            Id = Guid.NewGuid(),
            SupplierId = supplierId,
            Name = name,
            Email = email,
            Role = role,
            Phone = phone,
            IsPrimary = isPrimary
        };

        supplier.Contacts.Add(contact);
        await _context.SaveChangesAsync(cancellationToken);

        // Log audit
        await _auditService.LogChangeAsync(
            supplierId,
            "ADD_CONTACT",
            nameof(SupplierContact),
            contact.Id,
            null,
            contact,
            userId,
            userName,
            cancellationToken);

        // Invalidate cache
        await _cacheService.RemoveAsync($"supplier:{supplierId}", cancellationToken);

        _logger.LogInformation("Added contact {ContactId} to supplier {SupplierId}", contact.Id, supplierId);

        return contact;
    }

    public async Task<Supplier?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"supplier:{id}";
        var cached = await _cacheService.GetAsync<Supplier>(cacheKey, cancellationToken);
        if (cached is not null)
        {
            return cached;
        }

        var supplier = await _context.Suppliers
            .Include(s => s.Contacts)
            .Include(s => s.MaterialCategories)
            .Include(s => s.Capabilities)
            .Include(s => s.Certifications)
            .Include(s => s.Evaluations)
            .Include(s => s.OnboardingHistory)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (supplier is not null)
        {
            await _cacheService.SetAsync(cacheKey, supplier, cancellationToken: cancellationToken);
        }

        return supplier;
    }

    public async Task<(bool IsValid, Supplier? Supplier)> ValidateSupplierAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var supplier = await _context.Suppliers
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (supplier is null)
        {
            return (false, null);
        }

        return (true, supplier);
    }

    public async Task<(bool IsEligible, IReadOnlyList<string> Reasons)> CheckEligibilityAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var supplier = await _context.Suppliers
            .Include(s => s.Certifications)
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (supplier is null)
        {
            return (false, ["Supplier not found"]);
        }

        var reasons = new List<string>();

        // Check if supplier is Active
        if (supplier.Status != SupplierStatus.Active)
        {
            reasons.Add($"Supplier status is {supplier.Status}, must be Active");
        }

        // Check for expired certifications
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var expiredCerts = supplier.Certifications
            .Where(c => c.ExpirationDate.HasValue && c.ExpirationDate.Value < today)
            .ToList();

        if (expiredCerts.Count > 0)
        {
            reasons.Add($"Supplier has {expiredCerts.Count} expired certification(s)");
        }

        return (reasons.Count == 0, reasons);
    }

    public async Task<IReadOnlyList<MaterialCategory>> GetMaterialCategoriesAsync(CancellationToken cancellationToken = default)
    {
        var cacheKey = "material-categories";
        var cached = await _cacheService.GetAsync<List<MaterialCategory>>(cacheKey, cancellationToken);
        if (cached is not null)
        {
            return cached;
        }

        var categories = await _context.MaterialCategories
            .Where(c => c.IsActive)
            .OrderBy(c => c.Name)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        await _cacheService.SetAsync(cacheKey, categories, TimeSpan.FromMinutes(30), cancellationToken);

        return categories;
    }

    public async Task<Supplier> UpdateAsync(
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
        CancellationToken cancellationToken = default)
    {
        var supplier = await _context.Suppliers
            .Include(s => s.MaterialCategories)
            .Include(s => s.Capabilities)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (supplier is null)
        {
            throw new InvalidOperationException($"Supplier with ID '{id}' not found.");
        }

        // Check optimistic concurrency using UpdatedAt ticks
        if (supplier.UpdatedAt.Ticks != rowVersion)
        {
            throw new DbUpdateConcurrencyException("The supplier has been modified by another user.");
        }

        var oldSupplier = new { supplier.CompanyName, supplier.Address, supplier.City, supplier.Country, supplier.PostalCode };
        var changedFields = new List<string>();

        // Update fields if provided
        if (companyName is not null && companyName != supplier.CompanyName)
        {
            supplier.CompanyName = companyName;
            changedFields.Add("CompanyName");
        }
        if (address is not null && address != supplier.Address)
        {
            supplier.Address = address;
            changedFields.Add("Address");
        }
        if (city is not null && city != supplier.City)
        {
            supplier.City = city;
            changedFields.Add("City");
        }
        if (country is not null && country != supplier.Country)
        {
            supplier.Country = country;
            changedFields.Add("Country");
        }
        if (postalCode != supplier.PostalCode)
        {
            supplier.PostalCode = postalCode;
            changedFields.Add("PostalCode");
        }

        // Update material categories if provided
        if (materialCategoryIds is not null)
        {
            supplier.MaterialCategories.Clear();
            var categories = await _context.MaterialCategories
                .Where(c => materialCategoryIds.Contains(c.Id) && c.IsActive)
                .ToListAsync(cancellationToken);
            foreach (var category in categories)
            {
                supplier.MaterialCategories.Add(category);
            }
            changedFields.Add("MaterialCategories");
        }

        // Update capabilities if provided
        if (capabilities is not null)
        {
            // Remove old capabilities
            _context.SupplierCapabilities.RemoveRange(supplier.Capabilities);
            supplier.Capabilities.Clear();

            foreach (var capabilityName in capabilities)
            {
                supplier.Capabilities.Add(new SupplierCapability
                {
                    Id = Guid.NewGuid(),
                    SupplierId = supplier.Id,
                    Name = capabilityName,
                    IsActive = true
                });
            }
            changedFields.Add("Capabilities");
        }

        await _context.SaveChangesAsync(cancellationToken);

        // Log audit
        await _auditService.LogChangeAsync(
            supplier.Id,
            "UPDATE",
            nameof(Supplier),
            supplier.Id,
            oldSupplier,
            supplier,
            userId,
            userName,
            cancellationToken);

        // Invalidate cache
        await _cacheService.RemoveAsync($"supplier:{id}", cancellationToken);
        await _cacheService.InvalidateByTagAsync("suppliers", cancellationToken);

        // Publish event
        await _publishEndpoint.Publish(new SupplierUpdatedEvent(
            supplier.Id,
            supplier.CompanyName,
            changedFields,
            supplier.UpdatedAt,
            userId), cancellationToken);

        _logger.LogInformation("Updated supplier {SupplierId}", supplier.Id);

        return supplier;
    }

    public async Task<Supplier> UpdateStatusAsync(
        Guid id,
        SupplierStatus newStatus,
        string? reason,
        string userId,
        string userName,
        CancellationToken cancellationToken = default)
    {
        var supplier = await _context.Suppliers
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (supplier is null)
        {
            throw new InvalidOperationException($"Supplier with ID '{id}' not found.");
        }

        var oldStatus = supplier.Status;
        supplier.Status = newStatus;

        await _context.SaveChangesAsync(cancellationToken);

        // Log audit
        await _auditService.LogChangeAsync(
            supplier.Id,
            "STATUS_CHANGE",
            nameof(Supplier),
            supplier.Id,
            new { Status = oldStatus, Reason = reason },
            new { Status = newStatus },
            userId,
            userName,
            cancellationToken);

        // Invalidate cache
        await _cacheService.RemoveAsync($"supplier:{id}", cancellationToken);

        // Publish event
        await _publishEndpoint.Publish(new SupplierStatusChangedEvent(
            supplier.Id,
            oldStatus,
            newStatus,
            DateTime.UtcNow,
            userId), cancellationToken);

        _logger.LogInformation("Updated supplier {SupplierId} status from {OldStatus} to {NewStatus}", supplier.Id, oldStatus, newStatus);

        return supplier;
    }

    public async Task UpdateMetadataAsync(
        Guid id,
        DateTime? lastOrderDate,
        decimal? totalOrderValue,
        CancellationToken cancellationToken = default)
    {
        var supplier = await _context.Suppliers
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (supplier is null)
        {
            throw new InvalidOperationException($"Supplier with ID '{id}' not found.");
        }

        if (lastOrderDate.HasValue)
        {
            supplier.LastOrderDate = lastOrderDate;
        }
        if (totalOrderValue.HasValue)
        {
            supplier.TotalOrderValue = totalOrderValue;
        }

        await _context.SaveChangesAsync(cancellationToken);

        // Invalidate cache
        await _cacheService.RemoveAsync($"supplier:{id}", cancellationToken);

        _logger.LogDebug("Updated metadata for supplier {SupplierId}", supplier.Id);
    }

    public async Task<(IReadOnlyList<Supplier> Items, int TotalCount)> ListSuppliersAsync(
        int page,
        int pageSize,
        SupplierStatus? status,
        Guid? categoryId,
        string? capability,
        string? search,
        string sortBy,
        string sortOrder,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Suppliers
            .Include(s => s.MaterialCategories)
            .Include(s => s.Capabilities)
            .AsQueryable();

        // Apply filters
        if (status.HasValue)
        {
            query = query.Where(s => s.Status == status.Value);
        }

        if (categoryId.HasValue)
        {
            query = query.Where(s => s.MaterialCategories.Any(c => c.Id == categoryId.Value));
        }

        if (!string.IsNullOrWhiteSpace(capability))
        {
            query = query.Where(s => s.Capabilities.Any(c => c.Name.Contains(capability) && c.IsActive));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.ToLower();
            query = query.Where(s =>
                s.CompanyName.ToLower().Contains(searchLower) ||
                s.TaxId.ToLower().Contains(searchLower) ||
                s.City.ToLower().Contains(searchLower) ||
                s.Country.ToLower().Contains(searchLower));
        }

        // Get total count before pagination
        var totalCount = await query.CountAsync(cancellationToken);

        // Apply sorting
        query = sortBy.ToLower() switch
        {
            "companyname" => sortOrder.ToLower() == "desc"
                ? query.OrderByDescending(s => s.CompanyName)
                : query.OrderBy(s => s.CompanyName),
            "createdat" => sortOrder.ToLower() == "desc"
                ? query.OrderByDescending(s => s.CreatedAt)
                : query.OrderBy(s => s.CreatedAt),
            "updatedat" => sortOrder.ToLower() == "desc"
                ? query.OrderByDescending(s => s.UpdatedAt)
                : query.OrderBy(s => s.UpdatedAt),
            "status" => sortOrder.ToLower() == "desc"
                ? query.OrderByDescending(s => s.Status)
                : query.OrderBy(s => s.Status),
            "country" => sortOrder.ToLower() == "desc"
                ? query.OrderByDescending(s => s.Country)
                : query.OrderBy(s => s.Country),
            _ => query.OrderBy(s => s.CompanyName)
        };

        // Apply pagination
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<SupplierCertification> AddCertificationAsync(
        Guid supplierId,
        CertificationType documentType,
        string documentName,
        DateOnly issueDate,
        DateOnly? expirationDate,
        string? externalFileRef,
        string? notes,
        string userId,
        string userName,
        CancellationToken cancellationToken = default)
    {
        var supplier = await _context.Suppliers
            .FirstOrDefaultAsync(s => s.Id == supplierId, cancellationToken);

        if (supplier is null)
        {
            throw new InvalidOperationException($"Supplier with ID '{supplierId}' not found.");
        }

        var certification = new SupplierCertification
        {
            Id = Guid.NewGuid(),
            SupplierId = supplierId,
            DocumentType = documentType,
            DocumentName = documentName,
            IssueDate = issueDate,
            ExpirationDate = expirationDate,
            ExternalFileRef = externalFileRef,
            Notes = notes
        };

        _context.SupplierCertifications.Add(certification);
        await _context.SaveChangesAsync(cancellationToken);

        // Log audit
        await _auditService.LogChangeAsync(
            supplierId,
            "ADD_CERTIFICATION",
            nameof(SupplierCertification),
            certification.Id,
            null,
            certification,
            userId,
            userName,
            cancellationToken);

        // Invalidate cache
        await _cacheService.RemoveAsync($"supplier:{supplierId}", cancellationToken);

        _logger.LogInformation("Added certification {CertificationId} to supplier {SupplierId}", certification.Id, supplierId);

        return certification;
    }

    public async Task DeleteCertificationAsync(
        Guid supplierId,
        Guid certificationId,
        string userId,
        string userName,
        CancellationToken cancellationToken = default)
    {
        var certification = await _context.SupplierCertifications
            .FirstOrDefaultAsync(c => c.Id == certificationId && c.SupplierId == supplierId, cancellationToken);

        if (certification is null)
        {
            throw new InvalidOperationException($"Certification with ID '{certificationId}' not found for supplier '{supplierId}'.");
        }

        _context.SupplierCertifications.Remove(certification);
        await _context.SaveChangesAsync(cancellationToken);

        // Log audit
        await _auditService.LogChangeAsync(
            supplierId,
            "DELETE_CERTIFICATION",
            nameof(SupplierCertification),
            certificationId,
            certification,
            null,
            userId,
            userName,
            cancellationToken);

        // Invalidate cache
        await _cacheService.RemoveAsync($"supplier:{supplierId}", cancellationToken);

        _logger.LogInformation("Deleted certification {CertificationId} from supplier {SupplierId}", certificationId, supplierId);
    }

    public async Task<IReadOnlyList<(SupplierCertification Certification, Supplier Supplier, int DaysUntilExpiration)>> GetExpiringCertificationsAsync(
        int daysThreshold,
        CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var thresholdDate = today.AddDays(daysThreshold);

        var certifications = await _context.SupplierCertifications
            .Include(c => c.Supplier)
            .Where(c => c.ExpirationDate.HasValue &&
                        c.ExpirationDate.Value <= thresholdDate &&
                        c.ExpirationDate.Value >= today)
            .OrderBy(c => c.ExpirationDate)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return certifications
            .Select(c => (c, c.Supplier, c.ExpirationDate!.Value.DayNumber - today.DayNumber))
            .ToList();
    }

    public async Task<PerformanceEvaluation> AddEvaluationAsync(
        Guid supplierId,
        PerformanceRatingCategory category,
        int score,
        string? comments,
        DateOnly evaluationDate,
        string userId,
        string userName,
        CancellationToken cancellationToken = default)
    {
        var supplier = await _context.Suppliers
            .FirstOrDefaultAsync(s => s.Id == supplierId, cancellationToken);

        if (supplier is null)
        {
            throw new InvalidOperationException($"Supplier with ID '{supplierId}' not found.");
        }

        var evaluation = new PerformanceEvaluation
        {
            Id = Guid.NewGuid(),
            SupplierId = supplierId,
            RatingCategory = category,
            Score = score,
            Notes = comments,
            EvaluationDate = evaluationDate,
            EvaluatorId = userId,
            EvaluatorName = userName
        };

        _context.PerformanceEvaluations.Add(evaluation);
        await _context.SaveChangesAsync(cancellationToken);

        // Log audit
        await _auditService.LogChangeAsync(
            supplierId,
            "ADD_EVALUATION",
            nameof(PerformanceEvaluation),
            evaluation.Id,
            null,
            evaluation,
            userId,
            userName,
            cancellationToken);

        // Invalidate cache
        await _cacheService.RemoveAsync($"supplier:{supplierId}", cancellationToken);

        _logger.LogInformation("Added evaluation {EvaluationId} to supplier {SupplierId}", evaluation.Id, supplierId);

        return evaluation;
    }

    public async Task<IReadOnlyList<PerformanceEvaluation>> GetEvaluationsAsync(
        Guid supplierId,
        CancellationToken cancellationToken = default)
    {
        return await _context.PerformanceEvaluations
            .Where(e => e.SupplierId == supplierId)
            .OrderByDescending(e => e.EvaluationDate)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<Supplier> AdvanceOnboardingAsync(
        Guid supplierId,
        OnboardingStage targetStage,
        string? notes,
        string userId,
        string userName,
        CancellationToken cancellationToken = default)
    {
        var supplier = await _context.Suppliers
            .FirstOrDefaultAsync(s => s.Id == supplierId, cancellationToken);

        if (supplier is null)
        {
            throw new InvalidOperationException($"Supplier with ID '{supplierId}' not found.");
        }

        if (!OnboardingTransitions.IsValidTransition(supplier.OnboardingStage, targetStage))
        {
            var validStages = OnboardingTransitions.GetValidNextStages(supplier.OnboardingStage);
            throw new InvalidOperationException(
                $"Cannot transition from {supplier.OnboardingStage} to {targetStage}. Valid transitions: {string.Join(", ", validStages)}");
        }

        var oldStage = supplier.OnboardingStage;
        supplier.OnboardingStage = targetStage;

        // If reaching Active stage, also update supplier status
        if (targetStage == OnboardingStage.Active)
        {
            supplier.Status = SupplierStatus.Active;
        }

        // Record transition
        var onboardingStatus = new OnboardingStatus
        {
            Id = Guid.NewGuid(),
            SupplierId = supplierId,
            Stage = targetStage,
            TransitionedBy = userId,
            TransitionedByName = userName,
            Notes = notes
        };

        _context.OnboardingStatuses.Add(onboardingStatus);
        await _context.SaveChangesAsync(cancellationToken);

        // Log audit
        await _auditService.LogChangeAsync(
            supplierId,
            "ONBOARDING_TRANSITION",
            nameof(Supplier),
            supplierId,
            new { OnboardingStage = oldStage },
            new { OnboardingStage = targetStage },
            userId,
            userName,
            cancellationToken);

        // Invalidate cache
        await _cacheService.RemoveAsync($"supplier:{supplierId}", cancellationToken);

        _logger.LogInformation("Advanced supplier {SupplierId} onboarding from {OldStage} to {NewStage}", supplierId, oldStage, targetStage);

        return supplier;
    }

    public async Task<IReadOnlyList<OnboardingStatus>> GetOnboardingHistoryAsync(
        Guid supplierId,
        CancellationToken cancellationToken = default)
    {
        return await _context.OnboardingStatuses
            .Where(o => o.SupplierId == supplierId)
            .OrderByDescending(o => o.TransitionedAt)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<SupplierAuditLog> Items, int TotalCount)> GetAuditTrailAsync(
        Guid supplierId,
        DateTime? startDate,
        DateTime? endDate,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.SupplierAuditLogs
            .Where(a => a.SupplierId == supplierId);

        if (startDate.HasValue)
        {
            query = query.Where(a => a.Timestamp >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(a => a.Timestamp <= endDate.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(a => a.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task DeleteAsync(
        Guid id,
        string userId,
        string userName,
        CancellationToken cancellationToken = default)
    {
        var supplier = await _context.Suppliers
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (supplier is null)
        {
            throw new InvalidOperationException($"Supplier with ID '{id}' not found.");
        }

        // Log audit before deletion
        await _auditService.LogChangeAsync(
            id,
            "DELETE",
            nameof(Supplier),
            id,
            supplier,
            null,
            userId,
            userName,
            cancellationToken);

        _context.Suppliers.Remove(supplier);
        await _context.SaveChangesAsync(cancellationToken);

        // Invalidate cache
        await _cacheService.RemoveAsync($"supplier:{id}", cancellationToken);
        await _cacheService.InvalidateByTagAsync("suppliers", cancellationToken);

        _logger.LogInformation("Deleted supplier {SupplierId}", id);
    }
}
