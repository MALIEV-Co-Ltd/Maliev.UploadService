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
[Route("suppliers/v{version:apiVersion}/suppliers/{supplierId:guid}/contacts")]
[Authorize]
public class SupplierContactsController : ControllerBase
{
    private readonly ISupplierService _supplierService;
    private readonly IValidator<CreateContactRequest> _createValidator;
    private readonly ILogger<SupplierContactsController> _logger;

    public SupplierContactsController(
        ISupplierService supplierService,
        IValidator<CreateContactRequest> createValidator,
        ILogger<SupplierContactsController> logger)
    {
        _supplierService = supplierService;
        _createValidator = createValidator;
        _logger = logger;
    }

    /// <summary>
    /// Add contact to supplier
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ContactResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ContactResponse>> AddContact(
        Guid supplierId,
        [FromBody] CreateContactRequest request,
        CancellationToken cancellationToken)
    {
        var validationResult = await _createValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        var userId = User.FindFirst("sub")?.Value ?? "anonymous";
        var userName = User.FindFirst("name")?.Value ?? "Anonymous User";

        try
        {
            var contact = await _supplierService.AddContactAsync(
                supplierId,
                request.Name,
                request.Email,
                request.Role,
                request.Phone,
                false, // isPrimary - set via separate endpoint
                userId,
                userName,
                cancellationToken);

            var response = new ContactResponse(
                contact.Id,
                contact.Name,
                contact.Role,
                contact.Email,
                contact.Phone,
                contact.IsPrimary,
                contact.CreatedAt);

            return CreatedAtAction(
                nameof(GetContacts),
                new { supplierId, version = "1" },
                response);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// List contacts for supplier
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ContactResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ContactResponse>>> GetContacts(
        Guid supplierId,
        CancellationToken cancellationToken)
    {
        var supplier = await _supplierService.GetByIdAsync(supplierId, cancellationToken);
        if (supplier is null)
        {
            return NotFound(new { message = $"Supplier with ID {supplierId} not found" });
        }

        var contacts = supplier.Contacts.Select(c => new ContactResponse(
            c.Id,
            c.Name,
            c.Role,
            c.Email,
            c.Phone,
            c.IsPrimary,
            c.CreatedAt)).ToList();

        return Ok(contacts);
    }
}
