using RazorSlices;
using RshtmxApp.Shared.Models;
using RshtmxApp.Shared.Slices;

namespace RshtmxApp.Shared.Rendering;

public sealed class AppFeaturePageRenderer : IFeaturePageRenderer
{
    public IResult RenderPage(FeatureShellContext context)
    {
        var layout = CreateLayout(context);
        return Page.Create(new AppPageShellModel(layout, context.Detail));
    }

    public RazorSlice RenderNavigation(FeatureShellContext context) =>
        _TopNav.Create(CreateLayout(context));

    private static AppLayoutModel CreateLayout(FeatureShellContext context)
    {
        var title = $"{context.CurrentItem.Label} | RshtmxApp";
        return new AppLayoutModel(title, context.CurrentItem.Key, context.Navigation);
    }
}
