namespace RazorSlicesHtmx.Demo.Features.Items.Models;

[RazorSlicesHtmx.HtmlNames.GenerateHtmlNames]
public sealed class ItemUpsertRequest : IHaveValidationResult
{
    public int? Id { get; set; }

    public string? Code { get; set; }

    public string? Name { get; set; }

    public bool IsEnabled { get; set; }

    public IReadOnlyDictionary<string, (string errorCode, string errorMessage)[]> Errors { get; set; } =
        new Dictionary<string, (string errorCode, string errorMessage)[]>();
}
