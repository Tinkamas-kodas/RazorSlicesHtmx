using Apskaita.Web.Features.Admin.InvoiceTemplateRegistry.Models;
using FluentValidation;

namespace Apskaita.Web.Features.Admin.InvoiceTemplateRegistry.Validators;

public sealed class InvoiceTemplateUpsertRequestValidator : AbstractValidator<InvoiceTemplateUpsertRequest>
{
    public InvoiceTemplateUpsertRequestValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Code is required.")
            .MaximumLength(FieldLengths.InvoiceTemplate.Code).WithMessage($"Code must be {FieldLengths.InvoiceTemplate.Code} characters or fewer.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(FieldLengths.InvoiceTemplate.Name).WithMessage($"Name must be {FieldLengths.InvoiceTemplate.Name} characters or fewer.");

        RuleFor(x => x.LanguageCode)
            .NotEmpty().WithMessage("Language code is required.")
            .MaximumLength(FieldLengths.InvoiceTemplate.LanguageCode).WithMessage($"Language code must be {FieldLengths.InvoiceTemplate.LanguageCode} characters or fewer.");

        RuleFor(x => x.Content)
            .NotEmpty().WithMessage("Template content is required.")
            .MaximumLength(20000).WithMessage("Template content must be 20000 characters or fewer.");
    }
}

