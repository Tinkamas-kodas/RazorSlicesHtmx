namespace razr_slices_htmx2.Features.About.Models;

public sealed record AboutDetailModel(
    string Summary,
    IReadOnlyList<string> Changes,
    IReadOnlyList<string> Benefits);