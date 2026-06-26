namespace MyApp.Features.MyFeature.Models;

public sealed record MyFeatureListModel(
    IReadOnlyList<MyEntityRowModel> Items);
