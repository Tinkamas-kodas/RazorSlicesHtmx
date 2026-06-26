using RazorSlicesHtmx.HtmlNames;

namespace MyApp.Features.MyFeature.Models;

[GenerateHtmlNames]
public sealed class MyEntityUpsertRequest
{
    public int? Id { get; set; }
    public string? Name { get; set; }
    public bool IsEnabled { get; set; }
}
