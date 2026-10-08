namespace Apskaita.Entities;

public class CompanyInfo
{
    public string Name { get; set; } = string.Empty;
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
}
