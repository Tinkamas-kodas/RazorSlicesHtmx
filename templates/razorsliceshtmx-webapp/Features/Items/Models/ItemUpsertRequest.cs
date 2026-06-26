namespace RshtmxApp.Features.Items.Models;

[GenerateHtmlNames]
public sealed class ItemUpsertRequest : IHaveValidationResult
{
    public int? Id { get; set; }

    public string? Name { get; set; }

    public string? Description { get; set; }

    public bool IsActive { get; set; }

    public IReadOnlyDictionary<string, (string errorCode, string errorMessage)[]> Errors { get; set; } =
        new Dictionary<string, (string errorCode, string errorMessage)[]>();
}
