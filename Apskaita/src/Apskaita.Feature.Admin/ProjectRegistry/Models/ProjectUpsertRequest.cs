using Apskaita.Web.Shared.ProjectColors;

namespace Apskaita.Feature.Admin.ProjectRegistry.Models;

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

    private static readonly IReadOnlyDictionary<string, (string errorCode, string errorMessage)[]> EmptyErrors =
        new Dictionary<string, (string errorCode, string errorMessage)[]>();

    public static ProjectUpsertRequest CreateDefault() => new()
    {
        ColorRgb = ProjectColorCatalog.DefaultColorRgb,
        ColorLabel = ProjectColorCatalog.GetLookupText(ProjectColorCatalog.DefaultColorRgb),
        CurrencyCode = "EUR",
        IsActive = true,
        Errors = EmptyErrors
    };

    public static ProjectUpsertRequest FromEntity(Project row) => new()
    {
        Id = row.Id,
        ClientId = row.ClientId,
        Name = row.Name,
        Code = row.Code,
        HourlyRate = row.HourlyRate,
        ColorRgb = ProjectColorCatalog.NormalizeOrDefault(row.ColorRgb),
        ColorLabel = ProjectColorCatalog.GetLookupText(row.ColorRgb),
        CurrencyCode = row.CurrencyCode,
        InvoiceTemplateId = row.InvoiceTemplateId,
        IsActive = row.IsActive,
        Errors = EmptyErrors
    };

    public void Normalize()
    {
        Name = Name?.Trim();
        Code = Code?.Trim();
        ColorRgb = ProjectColorCatalog.Normalize(ColorRgb);
        ColorLabel = string.IsNullOrWhiteSpace(ColorLabel)
            ? ProjectColorCatalog.GetLookupText(ColorRgb)
            : ColorLabel.Trim();
        CurrencyCode = CurrencyCode?.Trim().ToUpperInvariant();
    }
}

