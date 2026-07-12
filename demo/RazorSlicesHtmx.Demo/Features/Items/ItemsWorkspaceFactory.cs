using RazorSlices;
using RazorSlicesHtmx.Demo.Features.Items.Slices;

namespace RazorSlicesHtmx.Demo.Features.Items;

public sealed class ItemsWorkspaceFactory() : BaseWorkspaceFactory("feature-details")
{
    protected override RazorSlice Wrap(RazorSlice inner) => FeatureWorkspace.Create(inner);
}
