using Apskaita.Entities;
using Apskaita.Web.Features.Admin.SystemConfig.Models;
using FluentValidation;

namespace Apskaita.Web.Features.Admin.SystemConfig.Validators;

public sealed class SystemSettingsUpsertRequestValidator : AbstractValidator<SystemSettingsUpsertRequest>
{
    public SystemSettingsUpsertRequestValidator()
    {
        RuleFor(x => x.DefaultVatRatePercent)
            .InclusiveBetween(0, 100).WithMessage("VAT rate must be between 0 and 100.");

        RuleFor(x => x.InvoiceNumberTemplate)
            .NotEmpty().WithMessage("Invoice number template is required.")
            .MaximumLength(FieldLengths.SystemSettings.InvoiceNumberTemplate).WithMessage($"Invoice number template must be {FieldLengths.SystemSettings.InvoiceNumberTemplate} characters or fewer.");
    }
}

