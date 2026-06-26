using RazorSlicesHtmx.AspNetCore.Models;
using RazorSlicesHtmx.AspNetCore.Results;
using MyApp.Features.MyFeature.Models;
using MyApp.Features.MyFeature.Services;
using MyApp.Features.MyFeature.Slices;

namespace MyApp.Features.MyFeature.Endpoints;

public sealed class MyFeatureEndpoints(FeatureResultBuilder resultBuilder) : BaseFeatureModule(resultBuilder)
{
    private readonly MyFeatureContentService _content = new();

    public override IReadOnlyList<NavigationItem> NavigationItems => [_content.CreateNavigationItem()];

    public override void MapEndpoints(WebApplication app)
    {
        app.MapGet("/my-feature-route/list", () =>
        {
            var model = _content.CreateListModel();
            return Result.For(_FeaturePage.Create(model))
                .AsFragment(_ListPage.Create(model))
                .BuildAsync();
        });

        app.MapGet("/my-feature-route/create", () =>
            Result.For(_CreatePage.Create(new MyEntityUpsertRequest()))
                .BuildAsync());

        app.MapPost("/my-feature-route/create", (MyEntityUpsertRequest request) =>
            {
                // TODO: validate and save
                return Result.For(_Empty.Create())
                    .WithTrigger("MyFeature.ListRefresh")
                    .WithToast($"MyEntity was created.")
                    .BuildAsync();
            })
            .DisableAntiforgery();
    }
}
