using Asp.Versioning;
using Maliev.SupplierService.Api.DTOs.Requests;
using Maliev.SupplierService.Api.DTOs.Responses;
using Maliev.SupplierService.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Maliev.SupplierService.Api.Controllers;

[ApiController]
[ApiVersion("1")]
[Route("suppliers/v{version:apiVersion}/suppliers/{supplierId:guid}/onboarding")]
[Authorize]
public class SupplierOnboardingController : ControllerBase
{
    private readonly ISupplierService _supplierService;
    private readonly ILogger<SupplierOnboardingController> _logger;

    public SupplierOnboardingController(
        ISupplierService supplierService,
        ILogger<SupplierOnboardingController> logger)
    {
        _supplierService = supplierService;
        _logger = logger;
    }

    /// <summary>
    /// Advance supplier onboarding stage
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(SupplierResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SupplierResponse>> AdvanceOnboarding(
        Guid supplierId,
        [FromBody] AdvanceOnboardingRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirst("sub")?.Value ?? "anonymous";
        var userName = User.FindFirst("name")?.Value ?? "Anonymous User";

        try
        {
            var supplier = await _supplierService.AdvanceOnboardingAsync(
                supplierId,
                request.TargetStage,
                request.Notes,
                userId,
                userName,
                cancellationToken);

            return Ok(new SupplierResponse(
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
                supplier.UpdatedAt.Ticks.ToString()));
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Cannot transition"))
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Get onboarding history for supplier
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(OnboardingHistoryResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<OnboardingHistoryResponse>> GetOnboardingHistory(
        Guid supplierId,
        CancellationToken cancellationToken)
    {
        var supplier = await _supplierService.GetByIdAsync(supplierId, cancellationToken);
        if (supplier is null)
        {
            return NotFound(new { message = $"Supplier with ID {supplierId} not found" });
        }

        var history = await _supplierService.GetOnboardingHistoryAsync(supplierId, cancellationToken);

        var items = history.Select(h => new OnboardingStatusResponse(
            h.Id,
            h.Stage,
            h.TransitionedAt,
            h.TransitionedBy,
            h.TransitionedByName,
            h.Notes)).ToList();

        return Ok(new OnboardingHistoryResponse(supplier.OnboardingStage, items));
    }
}
