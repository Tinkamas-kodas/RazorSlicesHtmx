namespace Apskaita.Entities;

public static class FieldLengths
{
    public static class CompanyInfo
    {
        public const int Name = 200;
        public const int CompanyCode = 50;
        public const int VatCode = 50;
        public const int AddressLine1 = 200;
        public const int AddressLine2 = 200;
        public const int City = 100;
        public const int PostalCode = 20;
        public const int CountryCode = 3;
        public const int ContactName = 200;
        public const int ContactEmail = 254;
        public const int ContactPhone = 50;
        public const int BankName = 200;
        public const int BankAccount = 34;
        public const int BankSwift = 11;
        public const int VatPayerCode = 50;
    }

    public static class User
    {
        public const int SubjectId = 100;
        public const int Provider = 20;
        public const int Email = 254;
        public const int DisplayName = 200;
    }

    public static class InvoiceTemplate
    {
        public const int Code = 50;
        public const int Name = 200;
        public const int LanguageCode = 3;
    }

    public static class Project
    {
        public const int Name = 200;
        public const int Code = 50;
        public const int ColorRgb = 7;
        public const int CurrencyCode = 3;
    }

    public static class WorkAct
    {
        public const int Number = 50;
        public const int CurrencyCode = 3;
    }

    public static class WorkActLine
    {
        public const int ExternalId = 100;
        public const int Description = 500;
    }

    public static class Unit
    {
        public const int Code = 50;
        public const int Name = 200;
    }

    public static class Invoice
    {
        public const int Number = 50;
        public const int CurrencyCode = 3;
        public const int LanguageCode = 3;
    }

    public static class InvoiceLine
    {
        public const int Description = 500;
    }

    public static class SystemSettings
    {
        public const int InvoiceNumberTemplate = 100;
    }

    public static class InvoiceSequenceState
    {
        public const int PrefixContext = 50;
    }

    public static class TimeTracking
    {
        public const int Description = 1000;
    }
}
