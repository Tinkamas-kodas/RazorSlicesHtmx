using Apskaita.Feature.Admin.IssuerRegistry.Slices;

namespace Apskaita.Feature.Admin.IssuerRegistry;

public sealed class IssuerRegistryWorkspaceFactory : BaseWorkspaceFactory
{
    protected override RazorSlice Wrap(RazorSlice inner) => FeatureWorkspace.Create(inner);
}

