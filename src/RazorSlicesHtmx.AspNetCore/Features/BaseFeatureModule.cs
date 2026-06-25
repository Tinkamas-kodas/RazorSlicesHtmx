using RazorSlices;
using RazorSlicesHtmx.AspNetCore.Models;
using RazorSlicesHtmx.AspNetCore.Results;

namespace RazorSlicesHtmx.AspNetCore.Features;

public abstract class BaseFeatureModule(FeatureResultBuilder resultBuilder) : IFeatureModule
{
    protected ModuleResultFacade Result => new(resultBuilder, Page);

    public abstract PageDefinition Page { get; }

    public abstract void MapEndpoints(WebApplication app);

    protected sealed class ModuleResultFacade(FeatureResultBuilder resultBuilder, PageDefinition page)
    {
        public HttpRequest Request => resultBuilder.Request;

        public FeatureResultBuilder.Builder For(RazorSlice detail) =>
            resultBuilder.Create(page, detail);

        public RazorSlice Dialog(RazorSlice content) => resultBuilder.CreateDialog(content);
    }
}