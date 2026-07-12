namespace RazorSlicesHtmx.Demo.Features.About;

public sealed record AboutDetailModel(
    string Summary,
    IReadOnlyList<string> Changes,
    IReadOnlyList<string> Benefits);