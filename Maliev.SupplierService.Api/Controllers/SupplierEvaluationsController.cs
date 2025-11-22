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
[Route("suppliers/v{version:apiVersion}/suppliers/{supplierId:guid}/evaluations")]
[Authorize]
public class SupplierEvaluationsController : ControllerBase
{
    private readonly ISupplierService _supplierService;
    private readonly IValidator<CreateEvaluationRequest> _validator;
    private readonly ILogger<SupplierEvaluationsController> _logger;

    public SupplierEvaluationsController(
        ISupplierService supplierService,
        IValidator<CreateEvaluationRequest> validator,
        ILogger<SupplierEvaluationsController> logger)
    {
        _supplierService = supplierService;
        _validator = validator;
        _logger = logger;
    }

    /// <summary>
    /// Add performance evaluation to supplier
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(EvaluationResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EvaluationResponse>> AddEvaluation(
        Guid supplierId,
        [FromBody] CreateEvaluationRequest request,
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
            var evaluation = await _supplierService.AddEvaluationAsync(
                supplierId,
                request.Category,
                request.Score,
                request.Comments,
                request.EvaluationDate,
                userId,
                userName,
                cancellationToken);

            var response = new EvaluationResponse(
                evaluation.Id,
                evaluation.RatingCategory,
                evaluation.Score,
                evaluation.Notes,
                evaluation.EvaluationDate,
                evaluation.EvaluatorId,
                evaluation.EvaluatorName,
                evaluation.CreatedAt);

            return CreatedAtAction(
                nameof(GetEvaluations),
                new { supplierId, version = "1" },
                response);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Get all evaluations for supplier
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(EvaluationListResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<EvaluationListResponse>> GetEvaluations(
        Guid supplierId,
        CancellationToken cancellationToken)
    {
        var evaluations = await _supplierService.GetEvaluationsAsync(supplierId, cancellationToken);

        var items = evaluations.Select(e => new EvaluationResponse(
            e.Id,
            e.RatingCategory,
            e.Score,
            e.Notes,
            e.EvaluationDate,
            e.EvaluatorId,
            e.EvaluatorName,
            e.CreatedAt)).ToList();

        var averageScore = items.Count > 0 ? (decimal?)items.Average(e => e.Score) : null;

        return Ok(new EvaluationListResponse(items, items.Count, averageScore));
    }
}
