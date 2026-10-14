using Apskaita.Feature.Admin.UnitRegistry.Slices;

namespace Apskaita.Feature.Admin.UnitRegistry;

public sealed class UnitRegistryWorkspaceFactory : BaseWorkspaceFactory
{
    protected override RazorSlice Wrap(RazorSlice inner) => FeatureWorkspace.Create(inner);
}

