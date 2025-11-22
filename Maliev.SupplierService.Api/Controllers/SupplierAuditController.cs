using Asp.Versioning;
using Maliev.SupplierService.Api.DTOs.Responses;
using Maliev.SupplierService.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Maliev.SupplierService.Api.Controllers;

[ApiController]
[ApiVersion("1")]
[Route("suppliers/v{version:apiVersion}/suppliers/{supplierId:guid}/audit")]
[Authorize]
public class SupplierAuditController : ControllerBase
{
    private readonly ISupplierService _supplierService;

    public SupplierAuditController(ISupplierService supplierService)
    {
        _supplierService = supplierService;
    }

    /// <summary>
    /// Get audit trail for supplier
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(AuditLogListResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<AuditLogListResponse>> GetAuditTrail(
        Guid supplierId,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        page = Math.Max(1, page);

        var (items, totalCount) = await _supplierService.GetAuditTrailAsync(
            supplierId, startDate, endDate, page, pageSize, cancellationToken);

        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var response = new AuditLogListResponse(
            items.Select(a => new AuditLogResponse(
                a.Id,
                a.ChangeType,
                a.EntityType,
                a.EntityId,
                a.OldValues,
                a.NewValues,
                a.ChangedBy,
                a.ChangedByName,
                a.Timestamp)).ToList(),
            totalCount,
            page,
            pageSize,
            totalPages);

        return Ok(response);
    }
}
