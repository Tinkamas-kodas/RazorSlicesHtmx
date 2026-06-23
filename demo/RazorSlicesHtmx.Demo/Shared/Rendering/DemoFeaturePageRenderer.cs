using RazorSlices;
using RazorSlicesHtmx.AspNetCore.Models;
using RazorSlicesHtmx.AspNetCore.Rendering;
using razr_slices_htmx2.Shared.Models;
using razr_slices_htmx2.Shared.Slices;

namespace razr_slices_htmx2.Shared.Rendering;

public sealed class DemoFeaturePageRenderer : IFeaturePageRenderer
{
    public IResult RenderPage(FeatureShellContext context)
    {
        var layout = CreateLayout(context);
        return Page.Create(new AppPageShellModel(layout, context.Detail));
    }

    public RazorSlice RenderNavigation(FeatureShellContext context) => _SidebarNav.Create(CreateLayout(context));

    private static AppLayoutModel CreateLayout(FeatureShellContext context)
    {
        var title = $"{context.CurrentPage.Label} | RazorSlices HTMX PoC";
        return new AppLayoutModel(title, context.CurrentPage.Key, context.Pages);
    }
}