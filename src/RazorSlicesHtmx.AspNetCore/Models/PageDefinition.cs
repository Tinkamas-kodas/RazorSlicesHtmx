using RazorSlices;

namespace RazorSlicesHtmx.AspNetCore.Models;

public sealed record PageDefinition(
    FeatureMetadata Metadata,
    Func<HttpContext, RazorSlice> CreateDetail)
{
    public PageDefinition(FeatureMetadata metadata, Func<RazorSlice> createDetail)
        : this(metadata, _ => createDetail())
    {
    }

    public string Key => Metadata.Key;

    public string Label => Metadata.Label;

    public string Route => Metadata.Route;

    public string Description => Metadata.Description;
}