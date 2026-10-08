using RazorSlicesHtmx.AspNetCore.Results;

namespace RazorSlicesHtmx.AspNetCore.Features;

public interface IHaveWorkspace
{
    string FeatureDivId { get; }

    Type WorkspaceFactoryType { get; }
}

public interface IHaveWorkspace<TWorkspaceFactory> : IHaveWorkspace
    where TWorkspaceFactory : BaseWorkspaceFactory
{
    Type IHaveWorkspace.WorkspaceFactoryType => typeof(TWorkspaceFactory);
}
