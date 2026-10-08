namespace RazorSlicesHtmx.Demo.Features.Lookup.Models;

public sealed record LookupSummaryModel(
    string Requester,
    string? ClientId,
    string? ClientText);
