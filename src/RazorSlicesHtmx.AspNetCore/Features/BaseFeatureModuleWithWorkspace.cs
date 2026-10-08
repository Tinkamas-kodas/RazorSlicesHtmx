using RazorSlicesHtmx.AspNetCore.Results;

namespace RazorSlicesHtmx.AspNetCore.Features;

public abstract class BaseFeatureModuleWithWorkspace<TWorkspaceFactory>(FeatureResultBuilder resultBuilder)
    : BaseFeatureModule(resultBuilder), IHaveWorkspace<TWorkspaceFactory>
    where TWorkspaceFactory : BaseWorkspaceFactory
{
    public abstract string FeatureDivId { get; }
}
