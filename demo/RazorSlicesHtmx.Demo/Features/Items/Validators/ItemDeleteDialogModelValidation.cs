using FluentValidation;
using RazorSlicesHtmx.Demo.Features.Items.Models;

namespace RazorSlicesHtmx.Demo.Features.Items.Validators;

public class ItemDeleteDialogModelValidation : AbstractValidator<ItemDeleteDialogModel>
{
    public ItemDeleteDialogModelValidation()
    {
        RuleFor(t => t.EnteredCode).NotEmpty();
        RuleFor(t => t.EnteredCode)
            .Must((c, p) => p.SequenceEqual(c.Code))
            .When(c=>!string.IsNullOrWhiteSpace(c.EnteredCode))
            .WithMessage("Entered code does not match the deleting item code");



    }
}