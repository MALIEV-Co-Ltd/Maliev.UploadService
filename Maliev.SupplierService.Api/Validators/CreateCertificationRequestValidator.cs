using FluentValidation;
using Maliev.SupplierService.Api.DTOs.Requests;

namespace Maliev.SupplierService.Api.Validators;

public class CreateCertificationRequestValidator : AbstractValidator<CreateCertificationRequest>
{
    public CreateCertificationRequestValidator()
    {
        RuleFor(x => x.DocumentType)
            .IsInEnum().WithMessage("Invalid document type");

        RuleFor(x => x.DocumentName)
            .NotEmpty().WithMessage("Document name is required")
            .MaximumLength(255).WithMessage("Document name must not exceed 255 characters");

        RuleFor(x => x.IssueDate)
            .NotEmpty().WithMessage("Issue date is required");

        RuleFor(x => x.ExpirationDate)
            .GreaterThanOrEqualTo(x => x.IssueDate)
            .When(x => x.ExpirationDate.HasValue)
            .WithMessage("Expiration date must be on or after issue date");

        RuleFor(x => x.ExternalFileRef)
            .MaximumLength(500).WithMessage("External file reference must not exceed 500 characters")
            .When(x => x.ExternalFileRef is not null);

        RuleFor(x => x.Notes)
            .MaximumLength(2000).WithMessage("Notes must not exceed 2000 characters")
            .When(x => x.Notes is not null);
    }
}
