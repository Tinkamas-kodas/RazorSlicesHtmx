using Apskaita.Feature.Admin.ProjectRegistry.Slices;

namespace Apskaita.Feature.Admin.ProjectRegistry;

public sealed class ProjectRegistryWorkspaceFactory : BaseWorkspaceFactory
{
    protected override RazorSlice Wrap(RazorSlice inner) => FeatureWorkspace.Create(inner);
}

