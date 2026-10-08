using RazorSlices;
using RazorSlicesHtmx.AspNetCore.Models;
using RazorSlicesHtmx.AspNetCore.Results;
using RazorSlicesHtmx.AspNetCore.Slices;
using Microsoft.AspNetCore.Routing;

namespace RazorSlicesHtmx.AspNetCore.Features;

public abstract class BaseFeatureModule(FeatureResultBuilder resultBuilder) : IFeatureModule
{
    protected ModuleResultFacade Result => new(
        resultBuilder,
        NavigationItems,
        this is IHaveWorkspace workspace ? (workspace.WorkspaceFactoryType, workspace.FeatureDivId) : null);

    public abstract IReadOnlyList<NavigationItem> NavigationItems { get; }

    public abstract void MapEndpoints(RouteGroupBuilder group);

    protected sealed class ModuleResultFacade(
        FeatureResultBuilder resultBuilder,
        IReadOnlyList<NavigationItem> items,
        (Type WorkspaceFactoryType, string FeatureDivId)? workspace)
    {
        public HttpRequest Request => resultBuilder.Request;

        public FeatureResultBuilder.Builder Empty()
        {
            var routeItem = FindFirstRouteItem(items)
                            ?? throw new InvalidOperationException(
                                "No NavigationItem with a route found in NavigationItems.");
            return resultBuilder.Create(routeItem, _Empty.Create());
        }

        public FeatureResultBuilder.Builder Page(RazorSlice detail)
        {
            var routeItem = FindFirstRouteItem(items)
                ?? throw new InvalidOperationException("No NavigationItem with a route found in NavigationItems.");
            return BuildPage(routeItem, detail);
        }

        public FeatureResultBuilder.Builder Page(NavigationItem routeItem, RazorSlice detail) =>
            BuildPage(routeItem, detail);

        public FeatureResultBuilder.Builder Fragment(RazorSlice detail)
        {
            var routeItem = FindFirstRouteItem(items)
                ?? throw new InvalidOperationException("No NavigationItem with a route found in NavigationItems.");
            return resultBuilder.Create(routeItem, detail);
        }

        public FeatureResultBuilder.Builder Fragment(NavigationItem routeItem, RazorSlice detail) =>
            resultBuilder.Create(routeItem, detail);

        public IResult ShowDialog(RazorSlice content) =>
            HtmxFragmentResult.Create(resultBuilder.CreateDialog(content))
                .WithTrigger("show-dialog")
                .Build();

        private FeatureResultBuilder.Builder BuildPage(NavigationItem routeItem, RazorSlice detail)
        {
            var builder = resultBuilder.Create(routeItem, detail);
            if (workspace is null)
            {
                return builder;
            }

            return builder.WithWorkspace(workspace.Value.WorkspaceFactoryType, workspace.Value.FeatureDivId);
        }

        

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