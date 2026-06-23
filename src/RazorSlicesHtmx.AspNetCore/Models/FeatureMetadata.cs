namespace RazorSlicesHtmx.AspNetCore.Models;

public sealed record FeatureMetadata(
    int NavigationOrder,
    string Key,
    string Label,
    string Route,
    string Description);