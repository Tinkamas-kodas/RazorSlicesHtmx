namespace Apskaita.Feature.Admin.ClientsRegistry.Models;

[GenerateHtmlNames]
public sealed partial class ClientUpsertRequest : IHaveValidationResult
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

    public static ClientUpsertRequest CreateDefault() => new()
    {
        IsActive = true,
        Errors = EmptyErrors
    };

    public static ClientUpsertRequest FromEntity(Client client) => new()
    {
        Id = client.Id,
        Name = client.Info.Name,
        CompanyCode = client.Info.CompanyCode,
        VatCode = client.Info.VatCode,
        AddressLine1 = client.Info.AddressLine1,
        AddressLine2 = client.Info.AddressLine2,
        City = client.Info.City,
        PostalCode = client.Info.PostalCode,
        CountryCode = client.Info.CountryCode,
        ContactName = client.Info.ContactName,
        ContactEmail = client.Info.ContactEmail,
        ContactPhone = client.Info.ContactPhone,
        BankName = client.Info.BankName,
        BankAccount = client.Info.BankAccount,
        BankSwift = client.Info.BankSwift,
        VatPayerCode = client.Info.VatPayerCode,
        IsActive = client.IsActive,
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
