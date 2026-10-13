namespace Apskaita.Web.Features.Admin.InvoiceTemplateRegistry.Models;

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
}

