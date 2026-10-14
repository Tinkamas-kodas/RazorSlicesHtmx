using Apskaita.Feature.Admin.InvoiceTemplateRegistry.Slices;

namespace Apskaita.Feature.Admin.InvoiceTemplateRegistry;

public sealed class InvoiceTemplateRegistryWorkspaceFactory : BaseWorkspaceFactory
{
    protected override RazorSlice Wrap(RazorSlice inner) => FeatureWorkspace.Create(inner);
}

