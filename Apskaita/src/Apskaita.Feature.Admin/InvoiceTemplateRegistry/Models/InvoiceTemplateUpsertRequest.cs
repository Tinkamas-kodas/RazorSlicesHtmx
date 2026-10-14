namespace Apskaita.Feature.Admin.InvoiceTemplateRegistry.Models;

[GenerateHtmlNames]
public sealed partial class InvoiceTemplateUpsertRequest : IHaveValidationResult
{
    public Guid? Id { get; set; }
    public string? Code { get; set; }
    public string? Name { get; set; }
    public string? LanguageCode { get; set; }
    public string? Content { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; }

    public IReadOnlyDictionary<string, (string errorCode, string errorMessage)[]> Errors { get; set; } =
        new Dictionary<string, (string errorCode, string errorMessage)[]>();

    private static readonly IReadOnlyDictionary<string, (string errorCode, string errorMessage)[]> EmptyErrors =
        new Dictionary<string, (string errorCode, string errorMessage)[]>();

    public static InvoiceTemplateUpsertRequest CreateDefault() => new()
    {
        IsActive = true,
        Errors = EmptyErrors
    };

    public static InvoiceTemplateUpsertRequest FromEntity(InvoiceTemplate row) => new()
    {
        Id = row.Id,
        Code = row.Code,
        Name = row.Name,
        LanguageCode = row.LanguageCode,
        Content = row.Content,
        IsDefault = row.IsDefault,
        IsActive = row.IsActive,
        Errors = EmptyErrors
    };

    public void Normalize()
    {
        Code = Code?.Trim();
        Name = Name?.Trim();
        LanguageCode = LanguageCode?.Trim().ToUpperInvariant();
        Content = Content?.Trim();
    }
}

