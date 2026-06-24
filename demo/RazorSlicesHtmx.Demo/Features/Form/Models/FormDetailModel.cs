namespace RazorSlicesHtmx.Demo.Features.Form.Models;

public sealed record FormDetailModel(
    string Summary,
    IReadOnlyList<string> IntentOptions,
    string PreviewMessage);