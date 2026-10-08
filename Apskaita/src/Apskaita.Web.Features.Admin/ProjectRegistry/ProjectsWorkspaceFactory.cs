using Apskaita.Web.Features.Admin.ProjectRegistry.Slices;

namespace Apskaita.Web.Features.Admin.ProjectRegistry;

public sealed class ProjectRegistryWorkspaceFactory : BaseWorkspaceFactory
{
    protected override RazorSlice Wrap(RazorSlice inner) => FeatureWorkspace.Create(inner);
}

