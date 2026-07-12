using RazorSlices;
using RazorSlicesHtmx.AspNetCore.Models;
using RazorSlicesHtmx.AspNetCore.Results;
using RazorSlicesHtmx.AspNetCore.Slices;

namespace RazorSlicesHtmx.AspNetCore.Features;

public abstract class BaseFeatureModule(FeatureResultBuilder resultBuilder) : IFeatureModule
{
    protected ModuleResultFacade Result => new(resultBuilder, NavigationItems);

    public abstract IReadOnlyList<NavigationItem> NavigationItems { get; }

    public abstract void MapEndpoints(WebApplication app);

    protected sealed class ModuleResultFacade(FeatureResultBuilder resultBuilder, IReadOnlyList<NavigationItem> items)
    {
        public HttpRequest Request => resultBuilder.Request;

        public FeatureResultBuilder.Builder Empty()
        {
            var routeItem = FindFirstRouteItem(items)
                            ?? throw new InvalidOperationException(
                                "No NavigationItem with a route found in NavigationItems.");
            return resultBuilder.Create(routeItem, _Empty.Create());
        }
        public FeatureResultBuilder.Builder For(RazorSlice detail)
        {
            var routeItem = FindFirstRouteItem(items)
                ?? throw new InvalidOperationException("No NavigationItem with a route found in NavigationItems.");
            return resultBuilder.Create(routeItem, detail);
        }

        public FeatureResultBuilder.Builder For(NavigationItem routeItem, RazorSlice detail) =>
            resultBuilder.Create(routeItem, detail);

        public IResult Dialog(RazorSlice content) =>
            HtmxFragmentResult.Create(resultBuilder.CreateDialog(content))
                .WithTriggerAfterSwap("show-dialog")
                .Build();

        private static NavigationItem? FindFirstRouteItem(IReadOnlyList<NavigationItem> items)
        {
            foreach (var item in items)
            {
                if (item is NavigationGroupItem group)
                {
                    var found = FindFirstRouteItem(group.Children);
                    if (found is not null) return found;
                }
                else
                {
                    return item;
                }
            }
            return null;
        }
    }
}