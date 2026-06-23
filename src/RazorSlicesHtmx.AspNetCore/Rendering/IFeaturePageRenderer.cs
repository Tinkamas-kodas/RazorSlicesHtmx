using RazorSlices;
using RazorSlicesHtmx.AspNetCore.Models;

namespace RazorSlicesHtmx.AspNetCore.Rendering;

public interface IFeaturePageRenderer
{
    IResult RenderPage(FeatureShellContext context);

    RazorSlice RenderNavigation(FeatureShellContext context);
}