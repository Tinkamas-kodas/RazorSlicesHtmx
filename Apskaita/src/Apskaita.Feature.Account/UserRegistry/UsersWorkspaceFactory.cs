using Apskaita.Feature.Account.UserRegistry.Slices;

namespace Apskaita.Feature.Account.UserRegistry;

public sealed class UserRegistryWorkspaceFactory : BaseWorkspaceFactory
{
    protected override RazorSlice Wrap(RazorSlice inner) => FeatureWorkspace.Create(inner);
}

