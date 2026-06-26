using FluentValidation;
using RshtmxApp.Features.Items.Models;

namespace RshtmxApp.Features.Items.Validators;

public sealed class ItemUpsertRequestValidator : AbstractValidator<ItemUpsertRequest>
{
    public ItemUpsertRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(100).WithMessage("Name must be 100 characters or fewer.");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("Description must be 500 characters or fewer.");
    }
}
