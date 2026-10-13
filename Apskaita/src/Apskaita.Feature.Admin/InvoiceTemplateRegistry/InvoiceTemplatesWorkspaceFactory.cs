using Apskaita.Web.Features.Admin.InvoiceTemplateRegistry.Slices;

namespace Apskaita.Web.Features.Admin.InvoiceTemplateRegistry;

public sealed class InvoiceTemplateRegistryWorkspaceFactory : BaseWorkspaceFactory
{
    protected override RazorSlice Wrap(RazorSlice inner) => FeatureWorkspace.Create(inner);
}

