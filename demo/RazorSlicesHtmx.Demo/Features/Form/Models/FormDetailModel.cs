namespace razr_slices_htmx2.Features.Form.Models;

public sealed record FormDetailModel(
    string Summary,
    IReadOnlyList<string> IntentOptions,
    string PreviewMessage);