using Apskaita.Web.Features.Admin.IssuerRegistry.Models;
using FluentValidation;

namespace Apskaita.Web.Features.Admin.IssuerRegistry.Validators;

public sealed class IssuerUpsertRequestValidator : AbstractValidator<IssuerUpsertRequest>
{
    public IssuerUpsertRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(FieldLengths.CompanyInfo.Name).WithMessage($"Name must be {FieldLengths.CompanyInfo.Name} characters or fewer.");

        RuleFor(x => x.CompanyCode)
            .MaximumLength(FieldLengths.CompanyInfo.CompanyCode).WithMessage($"Company code must be {FieldLengths.CompanyInfo.CompanyCode} characters or fewer.");

        RuleFor(x => x.VatCode)
            .MaximumLength(FieldLengths.CompanyInfo.VatCode).WithMessage($"VAT code must be {FieldLengths.CompanyInfo.VatCode} characters or fewer.");

        RuleFor(x => x.AddressLine1)
            .MaximumLength(FieldLengths.CompanyInfo.AddressLine1).WithMessage($"Address line 1 must be {FieldLengths.CompanyInfo.AddressLine1} characters or fewer.");

        RuleFor(x => x.AddressLine2)
            .MaximumLength(FieldLengths.CompanyInfo.AddressLine2).WithMessage($"Address line 2 must be {FieldLengths.CompanyInfo.AddressLine2} characters or fewer.");

        RuleFor(x => x.City)
            .MaximumLength(FieldLengths.CompanyInfo.City).WithMessage($"City must be {FieldLengths.CompanyInfo.City} characters or fewer.");

        RuleFor(x => x.PostalCode)
            .MaximumLength(FieldLengths.CompanyInfo.PostalCode).WithMessage($"Postal code must be {FieldLengths.CompanyInfo.PostalCode} characters or fewer.");

        RuleFor(x => x.CountryCode)
            .MaximumLength(FieldLengths.CompanyInfo.CountryCode).WithMessage($"Country code must be {FieldLengths.CompanyInfo.CountryCode} characters or fewer.");

        RuleFor(x => x.ContactName)
            .MaximumLength(FieldLengths.CompanyInfo.ContactName).WithMessage($"Contact name must be {FieldLengths.CompanyInfo.ContactName} characters or fewer.");

        RuleFor(x => x.ContactEmail)
            .MaximumLength(FieldLengths.CompanyInfo.ContactEmail).WithMessage($"Contact email must be {FieldLengths.CompanyInfo.ContactEmail} characters or fewer.")
            .EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.ContactEmail)).WithMessage("Contact email format is invalid.");

        RuleFor(x => x.ContactPhone)
            .MaximumLength(FieldLengths.CompanyInfo.ContactPhone).WithMessage($"Contact phone must be {FieldLengths.CompanyInfo.ContactPhone} characters or fewer.");

        RuleFor(x => x.BankName)
            .MaximumLength(FieldLengths.CompanyInfo.BankName).WithMessage($"Bank name must be {FieldLengths.CompanyInfo.BankName} characters or fewer.");

        RuleFor(x => x.BankAccount)
            .MaximumLength(FieldLengths.CompanyInfo.BankAccount).WithMessage($"Bank account must be {FieldLengths.CompanyInfo.BankAccount} characters or fewer.");

        RuleFor(x => x.BankSwift)
            .MaximumLength(FieldLengths.CompanyInfo.BankSwift).WithMessage($"SWIFT must be {FieldLengths.CompanyInfo.BankSwift} characters or fewer.");

        RuleFor(x => x.VatPayerCode)
            .MaximumLength(FieldLengths.CompanyInfo.VatPayerCode).WithMessage($"VAT payer code must be {FieldLengths.CompanyInfo.VatPayerCode} characters or fewer.");
    }
}

