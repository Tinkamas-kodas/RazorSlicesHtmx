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

    private static readonly IReadOnlyDictionary<string, (string errorCode, string errorMessage)[]> EmptyErrors =
        new Dictionary<string, (string errorCode, string errorMessage)[]>();

    public static UnitUpsertRequest CreateDefault() => new()
    {
        IsActive = true,
        Errors = EmptyErrors
    };

    public static UnitUpsertRequest FromEntity(Unit unit) => new()
    {
        Id = unit.Id,
        Code = unit.Code,
        Name = unit.Name,
        IsActive = unit.IsActive,
        Errors = EmptyErrors
    };

    public void Normalize()
    {
        Code = Code?.Trim();
        Name = Name?.Trim();
    }
}

