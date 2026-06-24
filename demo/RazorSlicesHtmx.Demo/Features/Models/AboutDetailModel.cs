namespace RazorSlicesHtmx.Demo.Features.About.Models;

public sealed record AboutDetailModel(
    string Summary,
    IReadOnlyList<string> Changes,
    IReadOnlyList<string> Benefits);