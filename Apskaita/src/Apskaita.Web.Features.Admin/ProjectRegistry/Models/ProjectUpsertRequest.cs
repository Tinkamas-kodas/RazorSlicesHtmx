namespace Apskaita.Web.Features.Admin.ProjectRegistry.Models;

[GenerateHtmlNames]
public sealed partial class ProjectUpsertRequest : IHaveValidationResult
{
    public Guid? Id { get; set; }
    public Guid? ClientId { get; set; }
    public string? Name { get; set; }
    public string? Code { get; set; }
    public int HourlyRate { get; set; }
    public string? ColorRgb { get; set; }
    public string? ColorLabel { get; set; }
    public string? CurrencyCode { get; set; }
    public Guid? InvoiceTemplateId { get; set; }
    public bool IsActive { get; set; }

    public List<ProjectOptionModel> Clients { get; set; } = [];
    public List<ProjectOptionModel> Templates { get; set; } = [];

    public IReadOnlyDictionary<string, (string errorCode, string errorMessage)[]> Errors { get; set; } =
        new Dictionary<string, (string errorCode, string errorMessage)[]>();
}

