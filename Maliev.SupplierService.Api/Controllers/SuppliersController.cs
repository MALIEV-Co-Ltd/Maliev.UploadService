using Asp.Versioning;
using FluentValidation;
using Maliev.SupplierService.Api.DTOs.Requests;
using Maliev.SupplierService.Api.DTOs.Responses;
using Maliev.SupplierService.Api.Services;
using Maliev.SupplierService.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Maliev.SupplierService.Api.Controllers;

[ApiController]
[ApiVersion("1")]
[Route("suppliers/v{version:apiVersion}/suppliers")]
[Authorize]
public class SuppliersController : ControllerBase
{
    private readonly ISupplierService _supplierService;
    private readonly IValidator<CreateSupplierRequest> _createValidator;
    private readonly ILogger<SuppliersController> _logger;

    public SuppliersController(
        ISupplierService supplierService,
        IValidator<CreateSupplierRequest> createValidator,
        ILogger<SuppliersController> logger)
    {
        _supplierService = supplierService;
        _createValidator = createValidator;
        _logger = logger;
    }

    /// <summary>
    /// Register a new supplier
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(SupplierResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SupplierResponse>> CreateSupplier(
        [FromBody] CreateSupplierRequest request,
        CancellationToken cancellationToken)
    {
        var validationResult = await _createValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        var userId = User.FindFirst("sub")?.Value ?? "anonymous";
        var userName = User.FindFirst("name")?.Value ?? "Anonymous User";

        var supplier = await _supplierService.CreateAsync(
            request.CompanyName,
            request.TaxId,
            request.Address,
            request.City,
            request.Country,
            request.PostalCode,
            request.MaterialCategoryIds,
            request.Capabilities,
            userId,
            userName,
            cancellationToken);

        // Add primary contact if provided
        if (request.PrimaryContact is not null)
        {
            await _supplierService.AddContactAsync(
                supplier.Id,
                request.PrimaryContact.Name,
                request.PrimaryContact.Email,
                request.PrimaryContact.Role,
                request.PrimaryContact.Phone,
                true, // isPrimary
                userId,
                userName,
                cancellationToken);
        }

        var response = MapToResponse(supplier);

        _logger.LogInformation("Created supplier {SupplierId}", supplier.Id);

        return CreatedAtAction(
            nameof(GetSupplier),
            new { id = supplier.Id, version = "1" },
            response);
    }

    /// <summary>
    /// List suppliers with pagination and filters
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(SupplierListResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<SupplierListResponse>> ListSuppliers(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] Data.Enums.SupplierStatus? status = null,
        [FromQuery] Guid? categoryId = null,
        [FromQuery] string? capability = null,
        [FromQuery] string? search = null,
        [FromQuery] string sortBy = "CompanyName",
        [FromQuery] string sortOrder = "asc",
        CancellationToken cancellationToken = default)
    {
        // Clamp page size
        pageSize = Math.Clamp(pageSize, 1, 100);
        page = Math.Max(1, page);

        var (items, totalCount) = await _supplierService.ListSuppliersAsync(
            page, pageSize, status, categoryId, capability, search, sortBy, sortOrder, cancellationToken);

        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var response = new SupplierListResponse(
            items.Select(MapToResponse).ToList(),
            totalCount,
            page,
            pageSize,
            totalPages);

        return Ok(response);
    }

    /// <summary>
    /// Get supplier details by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(SupplierDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SupplierDetailResponse>> GetSupplier(
        Guid id,
        CancellationToken cancellationToken)
    {
        var supplier = await _supplierService.GetByIdAsync(id, cancellationToken);

        if (supplier is null)
        {
            return NotFound(new { message = $"Supplier with ID {id} not found" });
        }

        var response = MapToDetailResponse(supplier);
        return Ok(response);
    }

    /// <summary>
    /// Validate supplier exists (for service-to-service integration)
    /// </summary>
    [HttpGet("{id:guid}/validate")]
    [ProducesResponseType(typeof(SupplierValidationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SupplierValidationResponse>> ValidateSupplier(
        Guid id,
        CancellationToken cancellationToken)
    {
        var (isValid, supplier) = await _supplierService.ValidateSupplierAsync(id, cancellationToken);

        if (!isValid || supplier is null)
        {
            return NotFound(new { message = $"Supplier with ID {id} not found" });
        }

        var response = new SupplierValidationResponse(
            supplier.Id,
            supplier.CompanyName,
            supplier.TaxId,
            supplier.Status,
            supplier.Status == Data.Enums.SupplierStatus.Active);

        return Ok(response);
    }

    /// <summary>
    /// Check supplier eligibility for purchase orders
    /// </summary>
    [HttpGet("{id:guid}/eligibility")]
    [ProducesResponseType(typeof(SupplierEligibilityResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SupplierEligibilityResponse>> CheckEligibility(
        Guid id,
        CancellationToken cancellationToken)
    {
        var (isEligible, reasons) = await _supplierService.CheckEligibilityAsync(id, cancellationToken);

        // If supplier not found, the reason will contain that info
        if (reasons.Count == 1 && reasons[0] == "Supplier not found")
        {
            return NotFound(new { message = $"Supplier with ID {id} not found" });
        }

        var response = new SupplierEligibilityResponse(id, isEligible, reasons);
        return Ok(response);
    }

    /// <summary>
    /// List all material categories
    /// </summary>
    [HttpGet("/suppliers/v{version:apiVersion}/categories")]
    [ProducesResponseType(typeof(MaterialCategoryListResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<MaterialCategoryListResponse>> GetCategories(
        CancellationToken cancellationToken)
    {
        var categories = await _supplierService.GetMaterialCategoriesAsync(cancellationToken);

        var response = new MaterialCategoryListResponse(
            categories.Select(c => new MaterialCategoryResponse(c.Id, c.Name, c.Description)).ToList());

        return Ok(response);
    }

    /// <summary>
    /// Update supplier information
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(SupplierResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SupplierResponse>> UpdateSupplier(
        Guid id,
        [FromBody] UpdateSupplierRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirst("sub")?.Value ?? "anonymous";
        var userName = User.FindFirst("name")?.Value ?? "Anonymous User";

        try
        {
            var rowVersion = long.Parse(request.RowVersion);
            var supplier = await _supplierService.UpdateAsync(
                id,
                request.CompanyName,
                request.Address,
                request.City,
                request.Country,
                request.PostalCode,
                request.MaterialCategoryIds,
                request.Capabilities,
                rowVersion,
                userId,
                userName,
                cancellationToken);

            return Ok(MapToResponse(supplier));
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException)
        {
            return Conflict(new { message = "The supplier has been modified by another user. Please refresh and try again." });
        }
    }

    /// <summary>
    /// Update supplier status
    /// </summary>
    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType(typeof(SupplierResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SupplierResponse>> UpdateStatus(
        Guid id,
        [FromBody] UpdateStatusRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirst("sub")?.Value ?? "anonymous";
        var userName = User.FindFirst("name")?.Value ?? "Anonymous User";

        try
        {
            var supplier = await _supplierService.UpdateStatusAsync(
                id,
                request.Status,
                request.Reason,
                userId,
                userName,
                cancellationToken);

            return Ok(MapToResponse(supplier));
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Update supplier metadata (for external service callbacks)
    /// </summary>
    [HttpPatch("{id:guid}/metadata")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateMetadata(
        Guid id,
        [FromBody] UpdateMetadataRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await _supplierService.UpdateMetadataAsync(
                id,
                request.LastOrderDate,
                request.TotalOrderValue,
                cancellationToken);

            return NoContent();
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Delete supplier
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteSupplier(
        Guid id,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirst("sub")?.Value ?? "anonymous";
        var userName = User.FindFirst("name")?.Value ?? "Anonymous User";

        try
        {
            await _supplierService.DeleteAsync(id, userId, userName, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return NotFound(new { message = ex.Message });
        }
    }

    private static SupplierResponse MapToResponse(Supplier supplier)
    {
        return new SupplierResponse(
            supplier.Id,
            supplier.CompanyName,
            supplier.TaxId,
            supplier.Address,
            supplier.City,
            supplier.Country,
            supplier.PostalCode,
            supplier.Status,
            supplier.OnboardingStage,
            supplier.CreatedAt,
            supplier.UpdatedAt,
            supplier.UpdatedAt.Ticks.ToString());
    }

    private static SupplierDetailResponse MapToDetailResponse(Supplier supplier)
    {
        return new SupplierDetailResponse(
            supplier.Id,
            supplier.CompanyName,
            supplier.TaxId,
            supplier.Address,
            supplier.City,
            supplier.Country,
            supplier.PostalCode,
            supplier.Status,
            supplier.OnboardingStage,
            supplier.CreatedAt,
            supplier.UpdatedAt,
            supplier.UpdatedAt.Ticks.ToString(),
            supplier.Contacts.Select(c => new ContactResponse(
                c.Id, c.Name, c.Role, c.Email, c.Phone, c.IsPrimary, c.CreatedAt)).ToList(),
            supplier.MaterialCategories.Select(m => new MaterialCategoryResponse(
                m.Id, m.Name, m.Description)).ToList(),
            supplier.Capabilities.Select(c => new CapabilityResponse(
                c.Id, c.Name, c.Description, c.IsActive)).ToList(),
            supplier.Certifications.Select(c => new CertificationResponse(
                c.Id, c.DocumentType.ToString(), c.DocumentName, c.IssueDate, c.ExpirationDate,
                c.ExternalFileRef,
                c.ExpirationDate.HasValue && c.ExpirationDate.Value < DateOnly.FromDateTime(DateTime.UtcNow),
                c.ExpirationDate.HasValue && c.ExpirationDate.Value <= DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
                c.CreatedAt)).ToList(),
            null // Performance summary computed separately
        );
    }
}
