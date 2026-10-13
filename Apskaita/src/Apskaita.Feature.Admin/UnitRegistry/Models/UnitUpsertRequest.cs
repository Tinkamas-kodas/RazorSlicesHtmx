namespace Apskaita.Web.Features.Admin.UnitRegistry.Models;

[GenerateHtmlNames]
public sealed partial class UnitUpsertRequest : IHaveValidationResult
{
    public Guid? Id { get; set; }
    public string? Code { get; set; }
    public string? Name { get; set; }
    public bool IsActive { get; set; }

    public IReadOnlyDictionary<string, (string errorCode, string errorMessage)[]> Errors { get; set; } =
        new Dictionary<string, (string errorCode, string errorMessage)[]>();
}

