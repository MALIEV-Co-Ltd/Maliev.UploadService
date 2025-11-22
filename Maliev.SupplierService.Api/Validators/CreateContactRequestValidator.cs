using FluentValidation;
using Maliev.SupplierService.Api.DTOs.Requests;

namespace Maliev.SupplierService.Api.Validators;

public class CreateContactRequestValidator : AbstractValidator<CreateContactRequest>
{
    public CreateContactRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Contact name is required")
            .MaximumLength(200).WithMessage("Contact name must not exceed 200 characters");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Invalid email format")
            .MaximumLength(255).WithMessage("Email must not exceed 255 characters");

        RuleFor(x => x.Role)
            .MaximumLength(100).WithMessage("Role must not exceed 100 characters")
            .When(x => x.Role is not null);

        RuleFor(x => x.Phone)
            .MaximumLength(50).WithMessage("Phone must not exceed 50 characters")
            .When(x => x.Phone is not null);
    }
}
