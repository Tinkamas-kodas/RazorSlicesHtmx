using Apskaita.Web.Features.Admin.SystemConfig.Slices;

namespace Apskaita.Web.Features.Admin.SystemConfig;

public sealed class SystemConfigWorkspaceFactory : BaseWorkspaceFactory
{
    protected override RazorSlice Wrap(RazorSlice inner) => FeatureWorkspace.Create(inner);
}

