namespace Apskaita.Feature.Admin.IssuerRegistry.Models;

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

    private static readonly IReadOnlyDictionary<string, (string errorCode, string errorMessage)[]> EmptyErrors =
        new Dictionary<string, (string errorCode, string errorMessage)[]>();

    public static IssuerUpsertRequest CreateDefault() => new()
    {
        IsActive = true,
        Errors = EmptyErrors
    };

    public static IssuerUpsertRequest FromEntity(Issuer issuer) => new()
    {
        Id = issuer.Id,
        Name = issuer.Info.Name,
        CompanyCode = issuer.Info.CompanyCode,
        VatCode = issuer.Info.VatCode,
        AddressLine1 = issuer.Info.AddressLine1,
        AddressLine2 = issuer.Info.AddressLine2,
        City = issuer.Info.City,
        PostalCode = issuer.Info.PostalCode,
        CountryCode = issuer.Info.CountryCode,
        ContactName = issuer.Info.ContactName,
        ContactEmail = issuer.Info.ContactEmail,
        ContactPhone = issuer.Info.ContactPhone,
        BankName = issuer.Info.BankName,
        BankAccount = issuer.Info.BankAccount,
        BankSwift = issuer.Info.BankSwift,
        VatPayerCode = issuer.Info.VatPayerCode,
        IsActive = issuer.IsActive,
        Errors = EmptyErrors
    };

    public void Normalize()
    {
        Name = Name?.Trim();
        CompanyCode = CompanyCode?.Trim();
        VatCode = VatCode?.Trim();
        AddressLine1 = AddressLine1?.Trim();
        AddressLine2 = AddressLine2?.Trim();
        City = City?.Trim();
        PostalCode = PostalCode?.Trim();
        CountryCode = CountryCode?.Trim();
        ContactName = ContactName?.Trim();
        ContactEmail = ContactEmail?.Trim();
        ContactPhone = ContactPhone?.Trim();
        BankName = BankName?.Trim();
        BankAccount = BankAccount?.Trim();
        BankSwift = BankSwift?.Trim();
        VatPayerCode = VatPayerCode?.Trim();
    }

    public CompanyInfo ToCompanyInfo() => new()
    {
        Name = Name ?? string.Empty,
        CompanyCode = CompanyCode,
        VatCode = VatCode,
        AddressLine1 = AddressLine1,
        AddressLine2 = AddressLine2,
        City = City,
        PostalCode = PostalCode,
        CountryCode = CountryCode,
        ContactName = ContactName,
        ContactEmail = ContactEmail,
        ContactPhone = ContactPhone,
        BankName = BankName,
        BankAccount = BankAccount,
        BankSwift = BankSwift,
        VatPayerCode = VatPayerCode
    };
}

