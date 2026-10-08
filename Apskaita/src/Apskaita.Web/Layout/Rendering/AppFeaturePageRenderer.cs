using Apskaita.Web.Layout.Models;
using Apskaita.Web.Layout.Slices;
using Apskaita.Web.Shared;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Apskaita.Web.Layout.Rendering;

public sealed class AppFeaturePageRenderer : IFeaturePageRenderer
{
    public IResult RenderPage(FeatureShellContext context)
    {
        var layout = CreateLayout(context);
        return _Page.Create(new AppPageShellModel(layout, context.Detail));
    }

    public RazorSlice RenderNavigation(FeatureShellContext context) =>
        _SideNav.Create(CreateLayout(context));

    private static AppLayoutModel CreateLayout(FeatureShellContext context)
    {
        var title = context.PageTitle ?? context.CurrentItem.Label;
        title = $"{title} | Apskaita.Web";
        var navigation = ToAdminNavigation(context.Navigation);
        return new AppLayoutModel(title, context.CurrentItem.Key, navigation);
    }

    private static IReadOnlyList<AdminNavItem> ToAdminNavigation(IReadOnlyList<NavigationItem> items)
    {
        var result = new List<AdminNavItem>();

        foreach (var item in items)
        {
            if (item is AdminNavItem nav)
            {
                result.Add(nav);
                continue;
            }

            if (item is NavigationGroupItem group)
            {
                foreach (var child in group.Children)
                {
                    if (child is AdminNavItem groupedNav)
                    {
                        result.Add(groupedNav);
                        continue;
                    }

                    result.Add(new AdminNavItem(group.Label, child.Key, child.Label, child.Route, child.Order, child.AuthorizationPolicy));
                }

                continue;
            }

            result.Add(new AdminNavItem("MAIN", item.Key, item.Label, item.Route, item.Order, item.AuthorizationPolicy));
        }

        return result;
    }
}
