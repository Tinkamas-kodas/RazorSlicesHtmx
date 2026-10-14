using Apskaita.Feature.Admin.UnitRegistry.Models;
using FluentValidation;

namespace Apskaita.Feature.Admin.UnitRegistry.Validators;

public sealed class UnitUpsertRequestValidator : AbstractValidator<UnitUpsertRequest>
{
    public UnitUpsertRequestValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Code is required.")
            .MaximumLength(FieldLengths.Unit.Code).WithMessage($"Code must be {FieldLengths.Unit.Code} characters or fewer.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(FieldLengths.Unit.Name).WithMessage($"Name must be {FieldLengths.Unit.Name} characters or fewer.");
    }
}

