namespace Apskaita.Web.Features.Admin.IssuerRegistry.Models;

[GenerateHtmlNames]
public sealed partial class IssuerUpsertRequest : IHaveValidationResult
{
    public Guid? Id { get; set; }

    public string? Name { get; set; }
    public string? CompanyCode { get; set; }
    public string? VatCode { get; set; }
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? PostalCode { get; set; }
    public string? CountryCode { get; set; }
    public string? ContactName { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string? BankName { get; set; }
    public string? BankAccount { get; set; }
    public string? BankSwift { get; set; }
    public string? VatPayerCode { get; set; }

    public bool IsActive { get; set; }

    public IReadOnlyDictionary<string, (string errorCode, string errorMessage)[]> Errors { get; set; } =
        new Dictionary<string, (string errorCode, string errorMessage)[]>();
}

