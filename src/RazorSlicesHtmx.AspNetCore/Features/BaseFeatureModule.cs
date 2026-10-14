using RazorSlices;
using RazorSlicesHtmx.AspNetCore.Models;
using RazorSlicesHtmx.AspNetCore.Results;
using RazorSlicesHtmx.AspNetCore.Slices;
using Microsoft.AspNetCore.Routing;

namespace RazorSlicesHtmx.AspNetCore.Features;

public abstract class BaseFeatureModule(FeatureResultBuilder resultBuilder) : IFeatureModule
{
    /// <summary>The injected HTMX result builder — pass it to section collaborators that compose results themselves.</summary>
    protected FeatureResultBuilder ResultBuilder => resultBuilder;

    /// <summary>
    /// Creates a nested route group under this feature's route group (e.g. for a feature mounted at
    /// <c>/items</c>, <c>MapSubgroup(group, "events")</c> yields routes under <c>/items/events/...</c>).
    /// The subgroup inherits the parent's authorization and conventions.
    /// </summary>
    protected static RouteGroupBuilder MapSubgroup(RouteGroupBuilder group, string name) => group.MapGroup(name);

    protected ModuleResultFacade Result => new(
        resultBuilder,
        NavigationItems,
        this is IHaveWorkspace workspace ? (workspace.WorkspaceFactoryType, workspace.FeatureDivId) : null);

    public abstract IReadOnlyList<NavigationItem> NavigationItems { get; }

    public abstract void MapEndpoints(RouteGroupBuilder group);

    /// <summary>
    /// The <c>Result</c> facade of <see cref="BaseFeatureModule"/>. Public so a feature can hand it to
    /// collaborator classes (route sections) instead of nesting them inside the module type.
    /// </summary>
    public sealed class ModuleResultFacade(
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