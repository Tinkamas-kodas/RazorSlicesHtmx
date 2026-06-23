using RazorSlices;

namespace RazorSlicesHtmx.AspNetCore.Models;

public sealed record FeatureShellContext(
    FeatureMetadata CurrentPage,
    IReadOnlyList<FeatureMetadata> Pages,
    RazorSlice Detail);