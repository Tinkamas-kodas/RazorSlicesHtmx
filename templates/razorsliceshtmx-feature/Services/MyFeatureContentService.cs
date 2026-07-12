using RazorSlicesHtmx.AspNetCore.Models;
using MyApp.Features.MyFeature.Models;

namespace MyApp.Features.MyFeature.Services;

public sealed class MyFeatureContentService
{
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
