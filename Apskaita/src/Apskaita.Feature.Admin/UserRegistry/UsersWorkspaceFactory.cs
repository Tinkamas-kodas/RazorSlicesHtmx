using Apskaita.Feature.Admin.UserRegistry.Slices;

namespace Apskaita.Feature.Admin.UserRegistry;

public sealed class UserRegistryWorkspaceFactory : BaseWorkspaceFactory
{
    protected override RazorSlice Wrap(RazorSlice inner) => FeatureWorkspace.Create(inner);
}

