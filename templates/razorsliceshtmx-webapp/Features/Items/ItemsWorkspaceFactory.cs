using RazorSlices;
using RazorSlicesHtmx.AspNetCore.Results;
using RshtmxApp.Features.Items.Slices;

namespace RshtmxApp.Features.Items;

public sealed class ItemsWorkspaceFactory() : BaseWorkspaceFactory("feature-details")
{
    protected override RazorSlice Wrap(RazorSlice inner) => FeatureWorkspace.Create(inner);
}
