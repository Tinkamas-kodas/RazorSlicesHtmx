using Apskaita.Web.Features.Admin.IssuerRegistry.Slices;

namespace Apskaita.Web.Features.Admin.IssuerRegistry;

public sealed class IssuerRegistryWorkspaceFactory : BaseWorkspaceFactory
{
    protected override RazorSlice Wrap(RazorSlice inner) => FeatureWorkspace.Create(inner);
}

