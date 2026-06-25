using RazorSlices;
using RazorSlicesHtmx.Demo.Shared.Models;
using RazorSlicesHtmx.Demo.Shared.Slices;

namespace RazorSlicesHtmx.Demo.Shared.Rendering;

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
        var title = $"{context.CurrentItem.Label} | RazorSlices HTMX PoC";
        return new AppLayoutModel(title, context.CurrentItem.Key, context.Navigation);
    }
}