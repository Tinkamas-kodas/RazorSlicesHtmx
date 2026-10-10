using Apskaita.Web.Features.Admin.ProjectRegistry.Models;
using FluentValidation;

namespace Apskaita.Web.Features.Admin.ProjectRegistry.Validators;

public sealed class ProjectUpsertRequestValidator : AbstractValidator<ProjectUpsertRequest>
{
    public ProjectUpsertRequestValidator()
    {
        RuleFor(x => x.ClientId)
            .NotNull().WithMessage("Client is required.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(FieldLengths.Project.Name).WithMessage($"Name must be {FieldLengths.Project.Name} characters or fewer.");

        RuleFor(x => x.Code)
            .MaximumLength(FieldLengths.Project.Code).WithMessage($"Code must be {FieldLengths.Project.Code} characters or fewer.");

        RuleFor(x => x.HourlyRate)
            .GreaterThanOrEqualTo(0).WithMessage("Hourly rate must be 0 or greater.");

        RuleFor(x => x.ColorRgb)
            .NotEmpty().WithMessage("Project color is required.")
            .MaximumLength(FieldLengths.Project.ColorRgb).WithMessage($"Project color must be {FieldLengths.Project.ColorRgb} characters.")
            .Matches("^#[0-9A-Fa-f]{6}$").WithMessage("Project color must be in #RRGGBB format.");

        RuleFor(x => x.CurrencyCode)
            .NotEmpty().WithMessage("Currency code is required.")
            .MaximumLength(FieldLengths.Project.CurrencyCode).WithMessage($"Currency code must be {FieldLengths.Project.CurrencyCode} characters or fewer.");

        RuleFor(x => x.InvoiceTemplateId)
            .NotNull().WithMessage("Invoice template is required.");
    }
}

