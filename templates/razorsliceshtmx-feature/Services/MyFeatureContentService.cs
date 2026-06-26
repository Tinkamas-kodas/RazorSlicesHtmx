using RazorSlicesHtmx.AspNetCore.Models;
using MyApp.Features.MyFeature.Models;
using MyApp.Features.MyFeature.Slices;

namespace MyApp.Features.MyFeature.Services;

public sealed class MyFeatureContentService
{
    public NavigationRouteItem CreateNavigationItem() => new(
        "myfeature",
        "MyFeature",
        "/my-feature-route",
        new PageDefinition(() => _FeaturePage.Create(CreateListModel())),
        Order: 100);

    public MyFeatureListModel CreateListModel()
    {
        // TODO: replace with real data source
        var items = new List<MyEntityRowModel>
        {
            new(1, "Sample Item", true)
        };
        return new MyFeatureListModel(items);
    }
}
