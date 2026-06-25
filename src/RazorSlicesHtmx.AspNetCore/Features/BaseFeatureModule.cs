using RazorSlices;
using RazorSlicesHtmx.AspNetCore.Models;
using RazorSlicesHtmx.AspNetCore.Results;

namespace RazorSlicesHtmx.AspNetCore.Features;

public abstract class BaseFeatureModule(FeatureResultBuilder resultBuilder) : IFeatureModule
{
    protected ModuleResultFacade Result => new(resultBuilder, NavigationItems);

    public abstract IReadOnlyList<NavigationItem> NavigationItems { get; }

    public abstract void MapEndpoints(WebApplication app);

    protected sealed class ModuleResultFacade(FeatureResultBuilder resultBuilder, IReadOnlyList<NavigationItem> items)
    {
        public HttpRequest Request => resultBuilder.Request;

        public FeatureResultBuilder.Builder For(RazorSlice detail)
        {
            var routeItem = FindFirstRouteItem(items)
                ?? throw new InvalidOperationException("No NavigationRouteItem found in NavigationItems.");
            return resultBuilder.Create(routeItem, detail);
        }

        public FeatureResultBuilder.Builder For(NavigationRouteItem routeItem, RazorSlice detail) =>
            resultBuilder.Create(routeItem, detail);

        public RazorSlice Dialog(RazorSlice content) => resultBuilder.CreateDialog(content);

        private static NavigationRouteItem? FindFirstRouteItem(IReadOnlyList<NavigationItem> items)
        {
            foreach (var item in items)
            {
                if (item is NavigationRouteItem route) return route;
                if (item is NavigationGroupItem group)
                {
                    var found = FindFirstRouteItem(group.Children);
                    if (found is not null) return found;
                }
            }
            return null;
        }
    }
}