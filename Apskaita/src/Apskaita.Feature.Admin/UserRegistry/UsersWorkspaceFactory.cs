using Apskaita.Web.Features.Admin.UserRegistry.Slices;

namespace Apskaita.Web.Features.Admin.UserRegistry;

public sealed class UserRegistryWorkspaceFactory : BaseWorkspaceFactory
{
    protected override RazorSlice Wrap(RazorSlice inner) => FeatureWorkspace.Create(inner);
}

