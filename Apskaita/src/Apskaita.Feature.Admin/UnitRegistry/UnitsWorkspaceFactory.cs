using Apskaita.Web.Features.Admin.UnitRegistry.Slices;

namespace Apskaita.Web.Features.Admin.UnitRegistry;

public sealed class UnitRegistryWorkspaceFactory : BaseWorkspaceFactory
{
    protected override RazorSlice Wrap(RazorSlice inner) => FeatureWorkspace.Create(inner);
}

