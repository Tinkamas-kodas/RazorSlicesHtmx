using Apskaita.Feature.Admin.SystemConfig.Slices;

namespace Apskaita.Feature.Admin.SystemConfig;

public sealed class SystemConfigWorkspaceFactory : BaseWorkspaceFactory
{
    protected override RazorSlice Wrap(RazorSlice inner) => FeatureWorkspace.Create(inner);
}

