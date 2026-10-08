namespace Apskaita.Web.Features.Admin.ClientsRegistry;

public sealed class ClientsWorkspaceFactory : BaseWorkspaceFactory
{
    protected override RazorSlice Wrap(RazorSlice inner) => Slices.FeatureWorkspace.Create(inner);
}
