namespace RazorSlicesHtmx.Demo.Features.Lookup.Models;

[RazorSlicesHtmx.AspNetCore.Html.GenerateHtmlNames]
[GenerateFormBinder]
public sealed partial class LookupDemoRequest
{
    public string? Requester { get; set; }

    public string? ClientId { get; set; }

    public string? ClientName { get; set; }
}
