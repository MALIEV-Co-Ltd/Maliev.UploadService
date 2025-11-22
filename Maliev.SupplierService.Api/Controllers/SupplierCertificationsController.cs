using Asp.Versioning;
using FluentValidation;
using Maliev.SupplierService.Api.DTOs.Requests;
using Maliev.SupplierService.Api.DTOs.Responses;
using Maliev.SupplierService.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Maliev.SupplierService.Api.Controllers;

[ApiController]
[ApiVersion("1")]
[Route("suppliers/v{version:apiVersion}/suppliers/{supplierId:guid}/certifications")]
[Authorize]
public class SupplierCertificationsController : ControllerBase
{
    private readonly ISupplierService _supplierService;
    private readonly IValidator<CreateCertificationRequest> _validator;
    private readonly ILogger<SupplierCertificationsController> _logger;

    public SupplierCertificationsController(
        ISupplierService supplierService,
        IValidator<CreateCertificationRequest> validator,
        ILogger<SupplierCertificationsController> logger)
    {
        _supplierService = supplierService;
        _validator = validator;
        _logger = logger;
    }

    /// <summary>
    /// Add certification to supplier
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(CertificationResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CertificationResponse>> AddCertification(
        Guid supplierId,
        [FromBody] CreateCertificationRequest request,
        CancellationToken cancellationToken)
    {
        var validationResult = await _validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        var userId = User.FindFirst("sub")?.Value ?? "anonymous";
        var userName = User.FindFirst("name")?.Value ?? "Anonymous User";

        try
        {
            var certification = await _supplierService.AddCertificationAsync(
                supplierId,
                request.DocumentType,
                request.DocumentName,
                request.IssueDate,
                request.ExpirationDate,
                request.ExternalFileRef,
                request.Notes,
                userId,
                userName,
                cancellationToken);

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var response = new CertificationResponse(
                certification.Id,
                certification.DocumentType.ToString(),
                certification.DocumentName,
                certification.IssueDate,
                certification.ExpirationDate,
                certification.ExternalFileRef,
                certification.ExpirationDate.HasValue && certification.ExpirationDate.Value < today,
                certification.ExpirationDate.HasValue && certification.ExpirationDate.Value <= today.AddDays(30),
                certification.CreatedAt);

            return CreatedAtAction(
                nameof(AddCertification),
                new { supplierId, id = certification.Id, version = "1" },
                response);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Delete certification from supplier
    /// </summary>
    [HttpDelete("{certificationId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteCertification(
        Guid supplierId,
        Guid certificationId,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirst("sub")?.Value ?? "anonymous";
        var userName = User.FindFirst("name")?.Value ?? "Anonymous User";

        try
        {
            await _supplierService.DeleteCertificationAsync(
                supplierId,
                certificationId,
                userId,
                userName,
                cancellationToken);

            return NoContent();
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return NotFound(new { message = ex.Message });
        }
    }
}

[ApiController]
[ApiVersion("1")]
[Route("suppliers/v{version:apiVersion}/certifications")]
[Authorize]
public class CertificationsController : ControllerBase
{
    private readonly ISupplierService _supplierService;

    public CertificationsController(ISupplierService supplierService)
    {
        _supplierService = supplierService;
    }

    /// <summary>
    /// Get certifications expiring within threshold days
    /// </summary>
    [HttpGet("expiring")]
    [ProducesResponseType(typeof(ExpiringCertificationsListResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ExpiringCertificationsListResponse>> GetExpiringCertifications(
        [FromQuery] int days = 30,
        CancellationToken cancellationToken = default)
    {
        days = Math.Clamp(days, 1, 365);

        var certifications = await _supplierService.GetExpiringCertificationsAsync(days, cancellationToken);

        var items = certifications.Select(c => new ExpiringCertificationResponse(
            c.Certification.Id,
            c.Supplier.Id,
            c.Supplier.CompanyName,
            c.Certification.DocumentType.ToString(),
            c.Certification.DocumentName,
            c.Certification.ExpirationDate!.Value,
            c.DaysUntilExpiration)).ToList();

        return Ok(new ExpiringCertificationsListResponse(items, items.Count));
    }
}
